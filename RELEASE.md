# 构建与发布

## 发布前置

- 版本号唯一来源是 `Directory.Build.props` 的 `<Version>`。
- 发布说明必须有：`docs/release-notes/v<版本>.md`，按 `release-notes-standard` 写。
- 构建与测试：`dotnet build FFXIVSubmarineLedger.sln`、`dotnet test FFXIVSubmarineLedger.sln`。
- NuGet 审计（NU1903）会把已知漏洞当错误，依赖版本住了就得升级，不要用 `-p:NuGetAudit=false` 绕。

## 产物

- `powershell -File tools/publish.ps1` → `artifacts/FFXIVSubmarineLedger-<版本>.zip` + `.sha256`（自包含目录包，约 59 MB）。
- 本机执行策略未设（等同 Restricted），跑脚本得加 `-ExecutionPolicy Bypass`：
  `powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish.ps1`。
- 脚本发现同名进程在跑就拒绝打包。用户装着旧版且正在用的时候（它跑在别的目录，互不影响），要么让用户关掉，要么单独跑一遍脚本里的命令，别直接杀掉用户的实例。
- 产物名不带架构段，只出 win-x64 一个包。
- 给人用的目录是解压出来的 `artifacts/run`。`artifacts/publish` 是打包中转目录，脚本每次整个删掉重建，别在里面直接跑 exe。

## 两条踩过的坑

- **不要改成单文件发布**：Tesseract 的 InteropDotNet 本地库加载器用 `Assembly.Location` 拼路径，单文件打包后是空值，构造引擎时抛空引用。已实测确认，不是推测。
- `tools/publish.ps1` 必须存成带 BOM 的 UTF-8：Windows PowerShell 5.1 会把无 BOM 的中文脚本读成 ANSI 导致语法错误。
- 不依赖 `Get-FileHash`：本机 PowerShell 5.1 环境里没有这个 cmdlet，脚本用 .NET 的 SHA256 直接算。

## 已发布

- v0.1.3（2026-11-28，修全新安装启动报 no such table: settings）。
- v0.1.2（2026-11-23，修艇名误读凭空造艇，补确认行的艇归属下拉）。
- v0.1.1（2026-11-22，只修识别与依赖漏洞，zip + sha256 两个附件）。
- v0.1.0（2026-11-02，首个正式版，zip + sha256 两个附件）。`/releases/latest` 已有内容，应用内更新检查不再回 404。
