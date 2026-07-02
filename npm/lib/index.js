'use strict';

// Windows 虚拟桌面控制 API。
// 内部通过 spawn 一个 .NET 桥接子进程（MacDesktop.Bridge.exe），
// 以 JSON Lines 协议通信，把 COM 能力封装成 Promise API。

const { spawn } = require('child_process');
const readline = require('readline');
const path = require('path');
const fs = require('fs');

/** 定位桥接 exe：优先环境变量，其次包内 bin/ 目录。 */
function resolveBridgePath(explicit) {
  const tried = [];
  const push = (p) => {
    if (p) {
      tried.push(p);
      if (fs.existsSync(p)) return p;
    }
    return null;
  };

  return (
    push(explicit) ||
    push(process.env.MACDESKTOP_BRIDGE) ||
    push(path.join(__dirname, '..', 'bin', 'MacDesktop.Bridge.exe')) ||
    (() => {
      throw new Error(
        '找不到 MacDesktop.Bridge.exe。请在 npm 包目录执行 `npm run build-bridge`（需 .NET 8 SDK），' +
          '或设置环境变量 MACDESKTOP_BRIDGE 指向 exe。已尝试：\n  ' +
          tried.join('\n  ')
      );
    })()
  );
}

/**
 * 把各种形式的窗口句柄统一为十进制字符串。
 * Electron 的 win.getNativeWindowHandle() 返回指针大小的小端 Buffer。
 */
function normalizeHwnd(hwnd) {
  if (Buffer.isBuffer(hwnd)) {
    if (hwnd.length >= 8) return hwnd.readBigUInt64LE(0).toString();
    if (hwnd.length >= 4) return BigInt(hwnd.readUInt32LE(0)).toString();
    throw new TypeError('hwnd Buffer 长度不足');
  }
  if (typeof hwnd === 'bigint') return hwnd.toString();
  if (typeof hwnd === 'number') return Math.trunc(hwnd).toString();
  if (typeof hwnd === 'string' && hwnd.trim() !== '') return hwnd.trim();
  throw new TypeError('hwnd 必须是 Buffer / number / bigint / 十进制字符串');
}

class VirtualDesktop {
  /**
   * @param {{ bridgePath?: string }} [options]
   */
  constructor(options = {}) {
    if (process.platform !== 'win32') {
      throw new Error('macdesktop 仅支持 Windows');
    }
    this._exe = resolveBridgePath(options.bridgePath);
    this._seq = 0;
    this._pending = new Map();
    this._closed = false;
    this._stderr = '';
    this._start();
  }

  _start() {
    this._proc = spawn(this._exe, [], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });

    this._ready = new Promise((resolve, reject) => {
      this._readyResolve = resolve;
      this._readyReject = reject;
    });
    // 未捕获时避免进程崩溃前的 unhandledRejection 噪音
    this._ready.catch(() => {});

    this._rl = readline.createInterface({ input: this._proc.stdout });
    this._rl.on('line', (line) => this._onLine(line));

    this._proc.stderr.on('data', (d) => {
      this._stderr += d.toString();
    });
    this._proc.on('error', (err) => this._failAll(err));
    this._proc.on('exit', (code, signal) => {
      if (this._closed) return;
      const msg =
        'MacDesktop.Bridge 进程退出 (code=' + code + ', signal=' + signal + ')' +
        (this._stderr ? '\n' + this._stderr.trim() : '');
      this._failAll(new Error(msg));
    });
  }

  _onLine(line) {
    let msg;
    try {
      msg = JSON.parse(line);
    } catch {
      return; // 非 JSON 行忽略
    }
    if (msg.event === 'ready') {
      this._readyResolve();
      return;
    }
    const p = this._pending.get(msg.id);
    if (!p) return;
    this._pending.delete(msg.id);
    if (msg.ok) p.resolve(msg.result);
    else p.reject(new Error(msg.error || 'bridge error'));
  }

  _failAll(err) {
    this._readyReject(err);
    for (const [, p] of this._pending) p.reject(err);
    this._pending.clear();
  }

  _call(method, params) {
    return this._ready.then(
      () =>
        new Promise((resolve, reject) => {
          if (this._closed) {
            reject(new Error('实例已释放 (dispose)'));
            return;
          }
          const id = ++this._seq;
          this._pending.set(id, { resolve, reject });
          this._proc.stdin.write(JSON.stringify({ id, method, params: params || {} }) + '\n');
        })
    );
  }

  /** 连通性测试，返回 "pong"。 */
  ping() {
    return this._call('ping');
  }

  /** 虚拟桌面总数。 */
  count() {
    return this._call('count');
  }

  /** 当前桌面索引（从 0 开始）。 */
  getCurrentIndex() {
    return this._call('currentIndex');
  }

  /** 列出所有桌面：[{ index, name, id }]。 */
  list() {
    return this._call('list');
  }

  /** 新建一个虚拟桌面，返回其索引。 */
  create() {
    return this._call('create');
  }

  /**
   * 删除指定桌面。
   * @param {number} index 要删除的桌面索引
   * @param {number} [fallbackIndex] 删除后上面的窗口迁移到的桌面；省略则自动选相邻桌面
   */
  remove(index, fallbackIndex) {
    return this._call('remove', { index, fallbackIndex });
  }

  /** 切换到指定桌面（带系统动画）。 */
  switchTo(index) {
    return this._call('switch', { index });
  }

  /** 读取指定桌面名称（未命名返回空串）。 */
  getName(index) {
    return this._call('getName', { index });
  }

  /** 设置指定桌面名称。 */
  setName(index, name) {
    return this._call('setName', { index, name });
  }

  /** 开关切换桌面时的动画（默认开）。 */
  setAnimation(enabled) {
    return this._call('setAnimation', { enabled: !!enabled });
  }

  /**
   * 把窗口移动到指定桌面。
   * @param {Buffer|number|bigint|string} hwnd 窗口句柄（Electron 用 win.getNativeWindowHandle()）
   * @param {number} index 目标桌面索引
   */
  moveWindow(hwnd, index) {
    return this._call('moveWindow', { hwnd: normalizeHwnd(hwnd), index });
  }

  /** 获取某窗口所在桌面的索引。 */
  getWindowDesktopIndex(hwnd) {
    return this._call('windowDesktopIndex', { hwnd: normalizeHwnd(hwnd) });
  }

  /** 判断某窗口是否在当前桌面上。 */
  isWindowOnCurrent(hwnd) {
    return this._call('isWindowOnCurrent', { hwnd: normalizeHwnd(hwnd) });
  }

  /** 释放：结束桥接子进程。 */
  dispose() {
    if (this._closed) return;
    this._closed = true;
    try {
      this._rl && this._rl.close();
    } catch {}
    try {
      this._proc && this._proc.kill();
    } catch {}
    for (const [, p] of this._pending) p.reject(new Error('实例已释放 (dispose)'));
    this._pending.clear();
  }
}

module.exports = { VirtualDesktop, normalizeHwnd };
