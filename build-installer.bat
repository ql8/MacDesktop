@echo off
REM ============================================================
REM  一键构建 MacDesktop 安装包
REM  1) 发布 self-contained 单文件 exe 到 publish-standalone\
REM  2) 调用 Inno Setup 编译器 (ISCC) 生成 installer\Output\MacDesktop-Setup.exe
REM ============================================================
setlocal

cd /d "%~dp0"

echo [1/2] 发布单文件 exe ...
dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o publish-standalone
if errorlevel 1 (
    echo 发布失败，已中止。
    exit /b 1
)

echo.
echo [2/2] 编译安装包 ...

REM 自动定位 Inno Setup 编译器 ISCC.exe
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"

if not defined ISCC (
    echo.
    echo [!] 未找到 Inno Setup 编译器 ISCC.exe
    echo     请先安装 Inno Setup 6: https://jrsoftware.org/isdl.php
    echo     安装后重新运行本脚本。
    exit /b 1
)

"%ISCC%" "installer\MacDesktop.iss"
if errorlevel 1 (
    echo 安装包编译失败。
    exit /b 1
)

echo.
echo 完成！安装包已生成：installer\Output\MacDesktop-Setup.exe
endlocal
