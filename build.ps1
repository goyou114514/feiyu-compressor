# 编译脚本：用系统自带的 .NET Framework 4.8 编译器打出单文件 exe
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 csc.exe' }

$out = Join-Path $root '肥鱼压缩器.exe'
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | Select-Object -ExpandProperty FullName
$assets = Join-Path $root 'assets'

$args = @(
    '/nologo',
    '/target:winexe',
    "/out:$out",
    "/win32icon:$assets\app.ico",
    "/resource:$assets\ZCOOLKuaiLe-Regular.ttf,ZCOOLKuaiLe-Regular.ttf",
    "/resource:$assets\bg.jpg,bg.jpg",
    "/resource:$assets\app.ico,app.ico",
    "/resource:$root\tools\7zr.exe,7zr.exe",
    '/optimize+',
    '/langversion:5',
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.IO.Compression.dll',
    '/r:System.IO.Compression.FileSystem.dll',
    '/r:System.Security.dll'
) + $src

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }
Write-Host "编译成功：$out"
