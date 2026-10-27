# FFXIVSubLedger

FF14 潜艇收益统计工具。收艇后截取游戏日志窗口，从日志里的入手行识别出物名与数量，按游戏内固定的卖店价算出本次收益，归档到角色/部队，并提供日均、月均、年均等统计。

当前正式支持 **Windows 10/11 x64**。

> 本项目是非官方社区工具，与 Square Enix 不存在隶属、授权、认可或赞助关系。

## 当前状态

**M1 骨架已完成**：解决方案分层、SQLite 持久化、多角色管理界面，界面框架对齐 FFXIVConfigManager。

**M2 截图入口已完成**：拖入、`Ctrl+V`、读剪贴板、选图片文件四种方式都能把截图收进来并预览，显示尺寸、来源与收益归属角色。

**识别路线已改（2026-10-27）**：从「截背包区域认图标」改为「截日志窗口认文字」，日语优先。改动的理由与新方案见 [docs/PLAN.md](docs/PLAN.md)，旧的网格检测与图标匹配整层退役，实测记录留在 PLAN 附录 A。

**M3 日语日志闭环进行中**。日志行格式已用真实样本核实（756 × 798、26 行）：入手行是 `[11:40] 角色名は ▶ 物名×5を入手した。`，HQ 物品在物名后多一个 `◉`，数量与物名同行、无空格分隔。

**文本行定位已完成**：`Infrastructure/Recognition/TextLineLocator.cs`，行投影成带 → 按行高中位数滤掉边缘亮边 → 叠框预览。真实样本上切出 26 行，左边界全为 13 px，行高 24–25 px，与独立量测脚本完全一致。

**日文 OCR 已接入**：Tesseract 5 + 随包分发的 `tessdata_fast` 日文模型（不依赖系统语言包）。真实样本 26 行全部读出。

**识别链已闭环**：行模板解析 + 词表纠错 + 按卖店价计价 + 逐行确认后写入账。真实样本认出 14 条入手行，物名与数量 14/14 正确，合计 779,378 吉尔（真值 779,434，差的 56 全部来自两条被 OCR 吞掉 HQ 标记的 `エメラルド`）。

**确认界面已完成**：每行可取消勾选、改数量、切 HQ（不可 HQ 的物名置灰）、在歧义时选候选、给词表外物名手填单价；合计实时刷新；「记入账」按「道具 + 品质」合并后写入 SQLite，写到默认角色名下。

**统计页已完成**：总收益 / 日均 / 月均 / 年均四张指标卡、收益走势图（日/月可切，自绘折线）、道具占比、角色占比、已入账记录列表（可逐条删除）、导出 CSV（一行一条明细，带 BOM 以避 Excel 乱码）。日均按自然日跨度算，不剔除没记录的日子。

**可以直接读聊天框了**：点「直接读聊天框」读一次，或者开「自动监听」每 3 秒轮询、收完艇自动出账。这条路从游戏客户端里拿文本，不走截图与 OCR，因此不会出现认错字或漏掉 HQ 标记的问题。需要以管理员身份运行；读不到时截图识别仍可用。

游戏的聊天缓冲是 1000 条的环形表，所以**先收完艇、再开程序也能补读**——只要这中间没有刷出 1000 条新聊天把那几行挤掉。

**设置页已完成**：显示数据目录与数据库位置、一键打开目录、JSON 备份导出与导入（导入整体替换，替换前自动备份当前数据）、显示掉落表条数与路径。

下一步是发布（单文件打包、GitHub Release、自动更新）与维度对比。

初版之后还要做「直接读取游戏内文本、彻底不截图」，需求与边界问题记录在 PLAN 第 12 节，本版不实现。

## 设计要点

- **离线**：潜艇产出全部卖店，价格由游戏内固定值决定，因此不查市场价、不联网取价、无价格缓存。HQ 物品卖店价固定高于 NQ（向上取整），价格表里只需存一份 NQ 价。
- **识别文字，再查词表**：日志把入手了什么、几个都写清楚了，而潜艇掉落物名是封闭的小集合。因此识别只需在词表里找出最像的那一条，OCR 不必完美，靠词表纠错与人工确认兜底。
- **语言分开做**：日志行模板与物名都随客户端语言变，每语言一个解析器加一份词表，日语先行。
- **不假设截图对齐**：不要求固定窗口尺寸或固定截取范围，行位置从图像内容里算。
- **日志窗口没有底板**：文字直接压在游戏画面上，二值化必须按行带自适应——实测背景亮度能从 8 跳到 180。
- **不触碰游戏**：不注入进程、不自动操作游戏、不解析游戏 DAT 文件。可选地从聊天框直接读文本（进程外只读），那样能避开 OCR 误识，但需要以管理员身份运行。
- **多种取图方式**：不强制用户先存成文件，剪贴板与拖放是主路径。

## 技术栈

- C# / .NET 10 LTS
- Avalonia UI 12 + CommunityToolkit.Mvvm
- SQLite（Microsoft.Data.Sqlite）
- Tesseract 5（日文 OCR，模型随程序分发）
- Sharlayan（进程外读取游戏聊天框，MIT）
- xUnit

## 目录结构

```text
src/FFXIVSubLedger.Domain            角色、部队、收益记录、道具、卖店价等领域模型
src/FFXIVSubLedger.Application       用例与仓储接口
src/FFXIVSubLedger.Infrastructure    SQLite 持久化、文本行解析（`Parsing`）、掉落表（`Catalog` + `Data/items.ja.json`）
src/FFXIVSubLedger.Platform.Windows  Windows 数据目录
src/FFXIVSubLedger.Desktop           Avalonia UI、MVVM、随包分发的 OCR 模型（`Data/tessdata`）
tests/                               自动化测试
tools/                               日志截图版式量测、窗口截图与剪贴板脚本（Python）
docs/PLAN.md                         技术方案
```

`Domain`、`Application` 与通用 `Infrastructure` 不依赖 Windows 专有 API，平台相关代码集中在 `Platform.Windows`。

## 构建

需要 .NET 10 SDK。

```powershell
dotnet build FFXIVSubLedger.sln
dotnet test FFXIVSubLedger.sln
dotnet run --project src/FFXIVSubLedger.Desktop
```

打一个可分发的包：

```powershell
powershell -File tools/publish.ps1
```

产物是 `artifacts/FFXIVSubLedger-<版本>.zip` 与同名 `.sha256`。自包含发布，目标机器不需要装 .NET。

**不用单文件发布**：Tesseract 的本地库加载器靠当前程序位置拼路径，单文件打包后那是空值，启动即崩。这一点已实测确认。

## 界面

窗口框架沿用同作者的 [FFXIVConfigManager](https://github.com/huaxianyan/FFXIVConfigManager)：

- `App.axaml` 只放 `FluentTheme` 与 `ViewLocator`，主题跟随系统；样式定义在 `Window.Styles` 或各视图内部，不集中到应用级资源。
- 左侧 220px 侧栏 + 右侧「页头 / 内容 / 底部说明」，页头承担页面标题与副标题。
- 卡片、分隔线、导航态统一使用半透明叠加色（`#0A808080` / `#40808080` / `#0E808080`），文字层级靠 `Opacity` 表达，因此浅色与深色主题都能直接用。
- 导航按 `Classes.nav="True"` + `Classes.active` 绑定当前页，页面切换靠 `IsVisible`。

界面截图见 [docs/ui-characters.png](docs/ui-characters.png)、[docs/ui-records.png](docs/ui-records.png) 与 [docs/ui-records-preview.png](docs/ui-records-preview.png)，由 `tools/capture_window.py` 抓取。

## 把截图交给程序

「收艇记账」页提供四种取图方式，不需要先存成文件：

| 方式 | 说明 |
| --- | --- |
| 拖入 | 把图片文件从资源管理器拖到页面任意位置，拖拽经过时拖放区会高亮 |
| `Ctrl + V` | 截图工具复制后直接粘贴。该快捷键只在收艇记账页生效，不影响其他页的文本输入 |
| 读取剪贴板 | 按钮触发，等价于 `Ctrl + V`；剪贴板里是图片文件时也会自动读取 |
| 选择图片文件 | 打开系统文件选择器，只列图片类型 |

取到图后会显示来源、像素尺寸与预览，并标出这笔收益将记到哪个角色名下。载入后预览会自动滚进视野，页面上的引导卡也会收起——有图了就不用再讲怎么把图弄进来。全局热键（收完艇按一下直接抓图）待做。

开发期可以用启动参数直接把一张图灌进来，省得反复手动拖：

```bash
FFXIVSubLedger.exe --image "D:\shots\bag.png"
```

## 识别路线

`Infrastructure/Recognition/` 放纯算法部分，不依赖 UI，可以单独测。目标流程是「日志截图 → 文本行 → 日文识别 → 行模板 → 词表纠错」，目前只完成第一步：

| 模块 | 职责 | 状态 |
| --- | --- | --- |
| 文本行定位 | `TextLineLocator`：行投影成带 → 按行高中位数滤掉边缘亮边 → 逐行算墨迹左右边界 | 已完成 |
| 日文 OCR | `LinePreprocessor`（裁行、放大 3×、Otsu 二值化、反相）+ `TesseractLineReader`；模型随包放在 `Data/tessdata/` | 已完成 |
| 行模板解析 | `JapaneseLogLineParser`：从 `[HH:MM] 角色名は ▶ 物名×Nを入手した。` 取物名与数量，容忍 OCR 把符号读歪 | 已完成 |
| 词表纠错 | `ItemMatcher`：封闭词表内编辑距离，多候选显式标出并按价降序默认 | 已完成 |
| 网格检测、图标提取与匹配 | v0.4 的旧路线，已退役，代码已删除 | 已删 |

日志行格式已用真实样本核实，具体形态、版式参数与遗留未知项见 [docs/PLAN.md](docs/PLAN.md) 第 2 节与 6.4。

量版式参数用：

```bash
python tools/measure_log_lines.py <日志截图.png>
```

不开窗口直接看一张图切出了哪些行：

```bash
FFXIVSubLedger.exe --dump-lines <图片> <输出.txt>
```

输出行带位置、行距、行高与左边距。纯标准库实现，不依赖 Pillow 与 numpy。

## 数据位置

```text
%LocalAppData%\FFXIVSubLedger\subledger.db            数据库
%LocalAppData%\FFXIVSubLedger\backups\                导入前的自动备份
```

设置页可以直接打开这个目录，也能把全部数据导出成一个可读的 JSON 文件。导入是整体替换，替换前会自动存一份当前数据。

## 开源许可证

除明确标注的第三方内容外，本项目的原创源代码与文档采用 [GNU General Public License v3.0 only](https://www.gnu.org/licenses/gpl-3.0.html)。

第三方组件：

- [Tesseract](https://github.com/tesseract-ocr/tesseract)（Apache-2.0），通过 NuGet 包 `Tesseract` 引入。
- [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) 的日文模型 `jpn.traineddata`（Apache-2.0），位于 `src/FFXIVSubLedger.Desktop/Data/tessdata/`。
- [Sharlayan](https://github.com/FFXIVAPP/sharlayan)（MIT），用于进程外读取游戏聊天框文本；它内置 FFXIVClientStructs（MIT）与 Lumina（MIT）。

OCR 引擎与模型都是随程序分发的，不依赖用户系统里装没装 OCR 组件或日文语言包。

## FINAL FANTASY XIV 与 Square Enix 声明

FINAL FANTASY XIV 及相关名称、商标、图像和其他游戏资源的权利归其各自权利人所有。

> FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
>
> © SQUARE ENIX
