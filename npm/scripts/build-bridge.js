'use strict';

// 构建桥接 exe 到 npm 包的 bin/ 目录。
// 需要本机已安装 .NET 8 SDK。默认发布为 self-contained 单文件，目标机无需装 .NET。

const { spawnSync } = require('child_process');
const path = require('path');

const proj = path.join(__dirname, '..', '..', 'bridge', 'MacDesktop.Bridge.csproj');
const outDir = path.join(__dirname, '..', 'bin');

const args = [
  'publish',
  proj,
  '-c',
  'Release',
  '-r',
  'win-x64',
  '--self-contained',
  'true',
  '-p:PublishSingleFile=true',
  '-p:IncludeNativeLibrariesForSelfExtract=true',
  '-o',
  outDir,
];

console.log('> dotnet ' + args.join(' '));
const r = spawnSync('dotnet', args, { stdio: 'inherit', shell: process.platform === 'win32' });

if (r.error) {
  console.error('调用 dotnet 失败，请确认已安装 .NET 8 SDK：https://dotnet.microsoft.com/download');
  console.error(r.error.message);
  process.exit(1);
}
process.exit(r.status || 0);
