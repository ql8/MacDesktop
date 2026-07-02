; MacDesktop 安装脚本 (Inno Setup 6+)
; 编译方式：
;   1) 安装 Inno Setup: https://jrsoftware.org/isdl.php
;   2) 先发布单文件 exe（见 build-installer.bat）
;   3) 用 Inno Setup 打开本文件点“Compile”，或运行 build-installer.bat 一键完成
; 产物：installer\Output\MacDesktop-Setup.exe

#define MyAppName "Mac Desktop"
; 版本号：CI 可用 ISCC /DMyAppVersion=1.2.3 覆盖；本地默认 1.0.0
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "MacDesktop"
#define MyAppExeName "MacDesktop.exe"

[Setup]
AppId={{8F3A6B21-4C7D-4E9A-9B12-MACDESKTOP001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\MacDesktop
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 单文件 self-contained 已含运行时，仅支持 64 位 Windows
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=MacDesktop-Setup
SetupIconFile=..\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; 需要写入 Program Files，要求管理员
PrivilegesRequired=admin
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; 想要简体中文向导界面：从 https://jrsoftware.org/files/istrans/ 下载 ChineseSimplified.isl
; 放到 Inno Setup 安装目录的 Languages\ 下，然后取消下一行注释：
; Name: "chs"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "autostart"; Description: "开机时自动启动 {#MyAppName}"; GroupDescription: "启动选项:"
Name: "startmenuicon"; Description: "创建开始菜单快捷方式"; GroupDescription: "快捷方式:"

[Files]
; 由 build-installer.bat 发布到 ..\publish-standalone\ 的单文件 exe
Source: "..\publish-standalone\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon

[Registry]
; 开机自启（全机 Run 键，适配管理员/全机安装）；勾选 autostart 任务时写入，卸载时删除
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "MacDesktop"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
; 安装完成后立即运行
Filename: "{app}\{#MyAppExeName}"; Description: "立即启动 {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载前先结束正在运行的进程，避免文件占用
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#MyAppExeName}"; \
    Flags: runhidden; RunOnceId: "KillMacDesktop"
