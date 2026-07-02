<!-- Language: English | [简体中文](./README.zh-CN.md) -->

# macdesktop

A **Windows Virtual Desktop** control API for **Electron / Node.js**: enumerate / create / switch / remove desktops, move windows to a specific desktop, and more.

> **Windows x64 only.** Virtual desktops depend on undocumented system COM interfaces. This package does not reimplement COM; instead it reuses a battle-tested C# wrapper and talks to it through a lightweight **bridge subprocess** (`MacDesktop.Bridge.exe`) over a JSON-lines protocol.

## Install & Setup

```bash
npm install macdesktop
```

The package does not ship a prebuilt exe. Before first use, build the bridge (requires the [.NET 8 SDK](https://dotnet.microsoft.com/download)):

```bash
npm run build-bridge --prefix node_modules/macdesktop
# or run `npm run build-bridge` from inside the package directory
```

The output is a self-contained single file `bin/MacDesktop.Bridge.exe`, so **the target machine does not need .NET installed**.
You can also set the `MACDESKTOP_BRIDGE` environment variable to point at an existing exe and skip the build.

## Quick Start

```js
const { VirtualDesktop } = require('macdesktop');

const vd = new VirtualDesktop();

console.log(await vd.count());            // total number of desktops
console.log(await vd.getCurrentIndex());  // current desktop index
console.log(await vd.list());             // [{ index, name, id }, ...]

const idx = await vd.create();            // create a desktop
await vd.switchTo(idx);                    // switch to it

vd.dispose();                              // release the subprocess when done
```

## Move a Window to a New Desktop in Electron

```js
const { VirtualDesktop } = require('macdesktop');

const vd = new VirtualDesktop();
const hwnd = win.getNativeWindowHandle(); // a Buffer, pass it in directly

const idx = await vd.create();
await vd.moveWindow(hwnd, idx);
await vd.switchTo(idx);

app.on('before-quit', () => vd.dispose());
```

See the full example in [`examples/electron-main.js`](./examples/electron-main.js).

## API

`new VirtualDesktop(options?)` — `options.bridgePath` overrides automatic exe detection.

| Method | Returns | Description |
| --- | --- | --- |
| `count()` | `Promise<number>` | Total number of virtual desktops |
| `getCurrentIndex()` | `Promise<number>` | Current desktop index (0-based) |
| `list()` | `Promise<DesktopInfo[]>` | All desktops `{ index, name, id }` |
| `create()` | `Promise<number>` | Create a desktop, returns its index |
| `remove(index, fallbackIndex?)` | `Promise<true>` | Remove a desktop; when `fallbackIndex` is omitted an adjacent one is chosen automatically |
| `switchTo(index)` | `Promise<true>` | Switch to the given desktop |
| `getName(index)` | `Promise<string>` | Read a desktop's name |
| `setName(index, name)` | `Promise<true>` | Set a desktop's name |
| `setAnimation(enabled)` | `Promise<true>` | Toggle the switch animation |
| `moveWindow(hwnd, index)` | `Promise<true>` | Move a window to the given desktop |
| `getWindowDesktopIndex(hwnd)` | `Promise<number>` | Index of the desktop a window is on |
| `isWindowOnCurrent(hwnd)` | `Promise<boolean>` | Whether a window is on the current desktop |
| `dispose()` | `void` | Terminate the bridge subprocess |

`hwnd` accepts a `Buffer` (Electron's `getNativeWindowHandle()`), a `number`, a `bigint`, or a decimal string.

## Notes

- **Architecture**: the Node side spawns a long-lived `MacDesktop.Bridge.exe` and communicates over JSON Lines on stdin/stdout; one instance maps to one subprocess.
- **Compatibility**: the COM interface GUIDs are hardcoded for a specific Windows build; a major update may require updating the wrapper in the bridge (see `VirtualDesktopApi.cs` in the root repo).
- **License**: MIT. COM wrapper based on Markus Scholtes / VirtualDesktop (MIT).
