// Electron 主进程示例：控制 Windows 虚拟桌面。
// 运行前先在 npm 包目录执行：npm run build-bridge
const { app, BrowserWindow } = require('electron');
const { VirtualDesktop } = require('macdesktop');

let vd;

app.whenReady().then(async () => {
  const win = new BrowserWindow({ width: 900, height: 600 });
  await win.loadURL('https://example.com');

  vd = new VirtualDesktop();

  console.log('桌面数量:', await vd.count());
  console.log('当前桌面索引:', await vd.getCurrentIndex());
  console.log('桌面列表:', await vd.list());

  // 新建一个桌面，把本窗口移过去并切过去（类似 macOS 进入独立 Space）
  const newIndex = await vd.create();
  const hwnd = win.getNativeWindowHandle();
  await vd.moveWindow(hwnd, newIndex);
  await vd.switchTo(newIndex);
  await vd.setName(newIndex, 'My Electron Space');
});

app.on('before-quit', () => {
  if (vd) vd.dispose();
});
