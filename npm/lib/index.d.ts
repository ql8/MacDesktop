/**
 * Windows 虚拟桌面控制 API（供 Electron / Node.js 使用）。
 * 仅在 win32 x64 上可用。
 */

export interface VirtualDesktopOptions {
  /** 显式指定 MacDesktop.Bridge.exe 路径，覆盖自动探测。 */
  bridgePath?: string;
}

export interface DesktopInfo {
  /** 桌面索引，从 0 开始。 */
  index: number;
  /** 桌面名称，未命名为空串。 */
  name: string;
  /** 桌面 GUID 字符串。 */
  id: string;
}

/** 支持的窗口句柄形式。Electron 用 win.getNativeWindowHandle() 得到 Buffer。 */
export type WindowHandle = Buffer | number | bigint | string;

export class VirtualDesktop {
  constructor(options?: VirtualDesktopOptions);

  /** 连通性测试，返回 "pong"。 */
  ping(): Promise<string>;
  /** 虚拟桌面总数。 */
  count(): Promise<number>;
  /** 当前桌面索引（从 0 开始）。 */
  getCurrentIndex(): Promise<number>;
  /** 列出所有桌面。 */
  list(): Promise<DesktopInfo[]>;
  /** 新建一个虚拟桌面，返回其索引。 */
  create(): Promise<number>;
  /**
   * 删除指定桌面。
   * @param index 要删除的桌面索引
   * @param fallbackIndex 删除后窗口迁移到的桌面；省略则自动选相邻桌面
   */
  remove(index: number, fallbackIndex?: number): Promise<true>;
  /** 切换到指定桌面（带系统动画）。 */
  switchTo(index: number): Promise<true>;
  /** 读取指定桌面名称。 */
  getName(index: number): Promise<string>;
  /** 设置指定桌面名称。 */
  setName(index: number, name: string): Promise<true>;
  /** 开关切换桌面动画（默认开）。 */
  setAnimation(enabled: boolean): Promise<true>;
  /** 把窗口移动到指定桌面。 */
  moveWindow(hwnd: WindowHandle, index: number): Promise<true>;
  /** 获取某窗口所在桌面索引。 */
  getWindowDesktopIndex(hwnd: WindowHandle): Promise<number>;
  /** 判断某窗口是否在当前桌面上。 */
  isWindowOnCurrent(hwnd: WindowHandle): Promise<boolean>;
  /** 释放：结束桥接子进程。 */
  dispose(): void;
}

/** 把各种句柄形式归一化为十进制字符串。 */
export function normalizeHwnd(hwnd: WindowHandle): string;
