<#
.SYNOPSIS
    打一个可分发的 Windows x64 包。

.DESCRIPTION
    自包含目录发布，然后去掉调试符号、压成 zip、生成校验文件。
    产物名按 `<项目名>-<版本号>.zip`：只出一个包，所以不带架构段。

    不用单文件发布：Tesseract 的本地库加载器靠当前程序位置拼路径，
    单文件打包后那是空的，构造引擎时直接抛空引用（详见 docs/PLAN.md 第 3 节）。

.PARAMETER Version
    包内版本号，默认取 Directory.Build.props 里的 <Version>。

.EXAMPLE
    powershell -File tools/publish.ps1
#>
[CmdletBinding()]
param(
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'artifacts/publish'
$dist = Join-Path $root 'artifacts'

if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
    $Version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw 'Directory.Build.props 里没有 <Version>，请显式传 -Version。'
    }
}

Write-Host "版本 $Version"

# 程序在跑的时候覆盖它的 exe 会得到「文件被占用」或者一个半新半旧的目录，两种都很难查。
$running = Get-Process -Name 'FFXIVSubmarineLedger' -ErrorAction SilentlyContinue
if ($running) {
    $ids = ($running | ForEach-Object { $_.Id }) -join ', '
    throw "程序还在运行（进程 $ids），先关掉再打包。"
}

if (Test-Path $output) { Remove-Item $output -Recurse -Force }

dotnet publish (Join-Path $root 'src/FFXIVSubmarineLedger.Desktop/FFXIVSubmarineLedger.Desktop.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $output

# 调试符号不进分发包。
Get-ChildItem $output -Filter *.pdb -File | Remove-Item -Force

$name = "FFXIVSubmarineLedger-$Version.zip"
$zip = Join-Path $dist $name
if (Test-Path $zip) { Remove-Item $zip -Force }

Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -CompressionLevel Optimal

# 校验文件按 `<算法>  <哈希>  <文件名>` 写，sha256sum 与人工比对都能直接用。
# 不依赖 Get-FileHash：部分环境（如被精简过的 PowerShell）没有这个 cmdlet。
$sha = [System.Security.Cryptography.SHA256]::Create()
$stream = [System.IO.File]::OpenRead($zip)
try {
    $hash = -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') })
}
finally {
    $stream.Dispose()
    $sha.Dispose()
}
$line = "SHA-256  $hash  $name`n"
[System.IO.File]::WriteAllText("$zip.sha256", $line, [System.Text.UTF8Encoding]::new($false))

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "产物 $zip（$sizeMb MB）"
Write-Host $line.TrimEnd()
