# Mac Desktop

在 Windows 上模拟 macOS 的「最大化即进入独立 Space」体验：

当某个窗口进入**最大化**（或**无边框全屏**）时，程序会自动：

1. 新建一个虚拟桌面
2. 把该窗口移动到新桌面
3. 切换到新桌面

当窗口**退出最大化**（或被关闭）时，程序会：

1. 把窗口移回原来的桌面
2. 切回原桌面
3. 删除临时创建的虚拟桌面

效果类似 macOS：双击标题栏最大化 → 自动进入一个新的 Space，退出最大化自动返回。

---

## 运行环境

- Windows 10 (build 19041 / 20H1) 或更高，推荐 **Windows 11**
- [.NET 8 SDK](https://dotnet.microsoft.com/download)（构建时需要）

> 虚拟桌面依赖的是系统未公开的 COM 接口，Windows 每个大版本可能变动。
> 本项目**不使用任何外部 NuGet 封装库**，而是在 `VirtualDesktopApi.cs` 中内嵌了针对当前
> build 硬编码接口 GUID 的 COM 封装（来源：Markus Scholtes / VirtualDesktop，MIT）。
> 这样规避了动态解析型库（如 Grabacr07/VirtualDesktop）常见的
> `The given key 'IApplicationView' was not present in the dictionary` 兼容性崩溃。
> 若某次 Windows 大版本更新后失效，需按新 build 更新 `VirtualDesktopApi.cs` 中的接口 GUID。

## 构建与运行

```bash
# 在项目根目录
dotnet restore
dotnet build -c Release

# 直接运行
dotnet run -c Release
```

或发布为独立可执行文件：

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
# 运行 publish/MacDesktop.exe
```

启动后程序常驻**系统托盘**（不显示窗口）。右键托盘图标：

- **已启用**：勾选/取消勾选来开关整体功能
- **处理无边框全屏**：是否把「铺满整个显示器的无边框窗口」也视为触发条件
- **退出**：退出程序并把仍在临时桌面上的窗口恢复回去

## 打包为安装包

程序常驻托盘，做成安装包可一并处理**开机自启、开始菜单快捷方式、卸载清理**。

前置：安装 [Inno Setup 6](https://jrsoftware.org/isdl.php)（免费）。

一键构建（发布单文件 exe + 编译安装包）：

```bash
build-installer.bat
```

产物：`installer\Output\MacDesktop-Setup.exe`。安装向导中可勾选「开机自启」与「创建开始菜单快捷方式」。

> 安装脚本见 `installer\MacDesktop.iss`，如需简体中文向导界面，按文件内注释下载 `ChineseSimplified.isl` 并取消对应行注释。

## GitHub 自动打包 (CI)

仓库已内置 GitHub Actions 工作流 `.github/workflows/build.yml`，每次构建都产出**两种版本**：

| 产物 | 说明 |
| --- | --- |
| `MacDesktop-Setup-vX.exe` | 安装包版：安装到 Program Files，可选开机自启/快捷方式，带卸载器 |
| `MacDesktop-vX-portable.exe` | 免安装版：self-contained 单文件，双击即用，无需装 .NET |

触发规则：

- **push 到 `main`/`master`** 或**手动触发**：构建后两个产物作为 **Artifact** 存档（在 Actions 运行页面下载）。
- **推送形如 `v1.2.3` 的 tag**：额外创建 **GitHub Release**，同时附带上述两个文件，版本号取自 tag。

发布一个正式版本示例：

```bash
git tag v1.0.0
git push origin v1.0.0
```

> 版本号通过 `ISCC /DMyAppVersion=...` 注入到安装包，无需改脚本。

## 工作原理

- 通过 `SetWinEventHook` 全局监听 `EVENT_OBJECT_LOCATIONCHANGE` 与 `EVENT_OBJECT_DESTROY`。
- 对每个顶层窗口检测最大化状态（`IsZoomed`）或是否铺满显示器（对比窗口矩形与显示器矩形）。
- 仅在**状态发生跳变**（正常→最大化 / 最大化→正常）时执行虚拟桌面操作，避免拖动/抖动导致的重复触发。
- 本程序自身发起的移动/切换操作期间会临时屏蔽事件，防止递归触发。

## 可调整项

在 `MaximizeWatcher.cs` 顶部的属性中调整：

| 属性 | 默认 | 说明 |
| --- | --- | --- |
| `HandleFullscreen` | `true` | 是否处理无边框全屏窗口 |
| `RequireCaption` | `true` | 仅处理带标题栏的窗口（关闭后可覆盖部分无边框全屏游戏/播放器） |
| `RemoveDesktopOnRestore` | `true` | 恢复后是否删除临时创建的虚拟桌面 |

## 已知限制

- 依赖系统内部 COM 接口，**Windows 大版本更新后可能需要更新 `VirtualDesktopApi.cs` 中的接口 GUID**。
- 部分**无边框全屏游戏**会独占显示（DirectX 全屏），此类窗口未必能被移动到其它虚拟桌面。
- 默认 `RequireCaption = true`，无标题栏的全屏程序不会被处理；如需覆盖可将其设为 `false`（可能带来误触发，请自行取舍）。
- 一个窗口在临时桌面时若被拖回其它桌面等手动操作，程序状态可能与实际不同步。
