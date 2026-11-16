# 构建与发布

## 发布前置

- 版本号唯一来源是 `Directory.Build.props` 的 `<Version>`。
- 发布说明必须有：`docs/release-notes/v<版本>.md`，按 `release-notes-standard` 写。
- 构建与测试：`dotnet build FFXIVSubmarineLedger.sln`、`dotnet test FFXIVSubmarineLedger.sln`。

## 产物

- `powershell -File tools/publish.ps1` → `artifacts/FFXIVSubmarineLedger-<版本>.zip` + `.sha256`（自包含目录包，约 54 MB）。
- 产物名不带架构段，只出 win-x64 一个包。
- 给人用的目录是解压出来的 `artifacts/run`。`artifacts/publish` 是打包中转目录，脚本每次整个删掉重建，别在里面直接跑 exe。

## 两条踩过的坑

- **不要改成单文件发布**：Tesseract 的 InteropDotNet 本地库加载器用 `Assembly.Location` 拼路径，单文件打包后是空值，构造引擎时抛空引用。已实测确认，不是推测。
- `tools/publish.ps1` 必须存成带 BOM 的 UTF-8：Windows PowerShell 5.1 会把无 BOM 的中文脚本读成 ANSI 导致语法错误。
- 不依赖 `Get-FileHash`：本机 PowerShell 5.1 环境里没有这个 cmdlet，脚本用 .NET 的 SHA256 直接算。

## 已发布

- v0.1.0（2026-11-02，首个正式版，zip + sha256 两个附件）。`/releases/latest` 已有内容，应用内更新检查不再回 404。
