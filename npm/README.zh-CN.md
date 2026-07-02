<!-- Language: [English](./README.md) | 简体中文 -->

# macdesktop

Windows **虚拟桌面 (Virtual Desktop)** 控制 API，供 **Electron / Node.js** 调用：枚举 / 新建 / 切换 / 删除桌面，把窗口移动到指定桌面等。

> 仅支持 **Windows x64**。虚拟桌面依赖系统未公开的 COM 接口，本包不重写 COM，而是复用经过实测的 C# 封装，通过一个轻量 **桥接子进程**（`MacDesktop.Bridge.exe`）以 JSON 行协议通信。

## 安装与准备

```bash
npm install macdesktop
```

包内不预置 exe，首次使用前需构建桥接程序（需 [.NET 8 SDK](https://dotnet.microsoft.com/download)）：

```bash
npm run build-bridge --prefix node_modules/macdesktop
# 或进入包目录执行 npm run build-bridge
```

构建产物为 self-contained 单文件 `bin/MacDesktop.Bridge.exe`，**目标机无需安装 .NET**。
也可用环境变量 `MACDESKTOP_BRIDGE` 指向已有的 exe，跳过构建。

## 快速上手

```js
const { VirtualDesktop } = require('macdesktop');

const vd = new VirtualDesktop();

console.log(await vd.count());            // 桌面总数
console.log(await vd.getCurrentIndex());  // 当前桌面索引
console.log(await vd.list());             // [{ index, name, id }, ...]

const idx = await vd.create();            // 新建桌面
await vd.switchTo(idx);                    // 切过去

vd.dispose();                              // 用完释放子进程
```

## 在 Electron 中把窗口移动到新桌面

```js
const { VirtualDesktop } = require('macdesktop');

const vd = new VirtualDesktop();
const hwnd = win.getNativeWindowHandle(); // Buffer，直接传入即可

const idx = await vd.create();
await vd.moveWindow(hwnd, idx);
await vd.switchTo(idx);

app.on('before-quit', () => vd.dispose());
```

完整示例见 [`examples/electron-main.js`](./examples/electron-main.js)。

## API

`new VirtualDesktop(options?)` — `options.bridgePath` 可覆盖 exe 自动探测。

| 方法 | 返回 | 说明 |
| --- | --- | --- |
| `count()` | `Promise<number>` | 虚拟桌面总数 |
| `getCurrentIndex()` | `Promise<number>` | 当前桌面索引（从 0 开始） |
| `list()` | `Promise<DesktopInfo[]>` | 所有桌面 `{ index, name, id }` |
| `create()` | `Promise<number>` | 新建桌面，返回索引 |
| `remove(index, fallbackIndex?)` | `Promise<true>` | 删除桌面；`fallbackIndex` 省略时自动选相邻 |
| `switchTo(index)` | `Promise<true>` | 切换到指定桌面 |
| `getName(index)` | `Promise<string>` | 读取桌面名 |
| `setName(index, name)` | `Promise<true>` | 设置桌面名 |
| `setAnimation(enabled)` | `Promise<true>` | 开关切换动画 |
| `moveWindow(hwnd, index)` | `Promise<true>` | 把窗口移到指定桌面 |
| `getWindowDesktopIndex(hwnd)` | `Promise<number>` | 窗口所在桌面索引 |
| `isWindowOnCurrent(hwnd)` | `Promise<boolean>` | 窗口是否在当前桌面 |
| `dispose()` | `void` | 结束桥接子进程 |

`hwnd` 接受 `Buffer`（Electron `getNativeWindowHandle()`）、`number`、`bigint` 或十进制字符串。

## 说明

- **架构**：Node 侧 spawn 一个常驻的 `MacDesktop.Bridge.exe`，二者用 stdin/stdout 上的 JSON Lines 通信，一次实例化对应一个子进程。
- **兼容性**：COM 接口 GUID 针对具体 Windows build 硬编码，大版本更新后可能需要更新桥接程序中的封装（见根仓库 `VirtualDesktopApi.cs`）。
- **许可**：MIT。COM 封装来源 Markus Scholtes / VirtualDesktop（MIT）。
