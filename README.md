<!-- Language: English | [简体中文](./README.zh-CN.md) -->

# Mac Desktop

Bring the macOS "maximize into its own Space" experience to Windows.

When a window becomes **maximized** (or goes **borderless fullscreen**), the app automatically:

1. Creates a new virtual desktop
2. Moves that window to the new desktop
3. Switches to the new desktop

When the window **leaves the maximized state** (or is closed), the app:

1. Moves the window back to its original desktop
2. Switches back to that desktop
3. Deletes the temporary virtual desktop it created

The result feels like macOS: double-click the title bar to maximize → you land in a fresh Space; exit maximize → you return automatically.

---

## Requirements

- Windows 10 (build 19041 / 20H1) or later, **Windows 11** recommended
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (needed to build)

> Virtual desktops rely on undocumented system COM interfaces that may change with every major Windows release.
> This project **does not depend on any external NuGet wrapper**. Instead, `VirtualDesktopApi.cs` embeds a COM
> wrapper with interface GUIDs hardcoded for the current build (source: Markus Scholtes / VirtualDesktop, MIT).
> This avoids the compatibility crashes common to dynamic-resolution libraries (e.g. Grabacr07/VirtualDesktop),
> such as `The given key 'IApplicationView' was not present in the dictionary`.
> If a major Windows update breaks it, update the interface GUIDs in `VirtualDesktopApi.cs` for the new build.

## Build & Run

```bash
# From the project root
dotnet restore
dotnet build -c Release

# Run directly
dotnet run -c Release
```

Or publish as a standalone executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
# Run publish/MacDesktop.exe
```

Once started, the app lives in the **system tray** (no window is shown). Right-click the tray icon:

- **Enabled**: toggle the whole feature on/off
- **Handle borderless fullscreen**: whether borderless windows that fill the entire monitor also count as a trigger
- **Exit**: quit the app and restore any windows still on temporary desktops

## Packaging an Installer

Since the app runs in the tray, an installer can also handle **auto-start on boot, Start Menu shortcuts, and uninstall cleanup**.

Prerequisite: install [Inno Setup 6](https://jrsoftware.org/isdl.php) (free).

One-click build (publish single-file exe + compile installer):

```bash
build-installer.bat
```

Output: `installer\Output\MacDesktop-Setup.exe`. The wizard offers "start on boot" and "create Start Menu shortcut" options.

> The install script is `installer\MacDesktop.iss`. For a Simplified Chinese wizard UI, download `ChineseSimplified.isl` and uncomment the relevant line as noted in the file.

## Automated Builds (CI)

The repo ships with a GitHub Actions workflow at `.github/workflows/build.yml` that produces **two flavors** on every build:

| Artifact | Description |
| --- | --- |
| `MacDesktop-Setup-vX.exe` | Installer edition: installs to Program Files, optional auto-start/shortcut, includes an uninstaller |
| `MacDesktop-vX-portable.exe` | Portable edition: self-contained single file, double-click to run, no .NET required |

Triggers:

- **Push to `main`/`master`** or **manual dispatch**: both artifacts are archived as **Artifacts** (downloadable from the Actions run page).
- **Push a tag like `v1.2.3`**: additionally creates a **GitHub Release** with both files attached; the version is taken from the tag.

Example of cutting a release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

> The version is injected into the installer via `ISCC /DMyAppVersion=...`; no script changes needed.

## How It Works

- Uses `SetWinEventHook` to globally listen for `EVENT_OBJECT_LOCATIONCHANGE` and `EVENT_OBJECT_DESTROY`.
- For each top-level window, detects the maximized state (`IsZoomed`) or whether it fills the monitor (comparing the window rect against the monitor rect).
- Virtual-desktop operations run **only on a state transition** (normal→maximized / maximized→normal), avoiding repeated triggers from dragging/jitter.
- Events are temporarily suppressed during the app's own move/switch operations to prevent recursive triggering.

## Tunables

Adjust the properties at the top of `MaximizeWatcher.cs`:

| Property | Default | Description |
| --- | --- | --- |
| `HandleFullscreen` | `true` | Whether to handle borderless fullscreen windows |
| `RequireCaption` | `true` | Only handle windows with a title bar (turning this off can cover some borderless fullscreen games/players) |
| `RemoveDesktopOnRestore` | `true` | Whether to delete the temporary virtual desktop after restoring |

## Known Limitations

- Relies on internal system COM interfaces, so **a major Windows update may require updating the interface GUIDs in `VirtualDesktopApi.cs`**.
- Some **borderless fullscreen games** take exclusive display ownership (DirectX fullscreen); such windows may not be movable to other virtual desktops.
- With the default `RequireCaption = true`, fullscreen programs without a title bar are ignored; set it to `false` to cover them (at the risk of false triggers — your call).
- If a window on a temporary desktop is manually dragged back to another desktop, the app's state may fall out of sync with reality.
