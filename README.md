# FFXIVSubmarineLedger

FF14 潜艇收益统计工具。收艇后从游戏聊天框里直接读出（也可截日志窗口走 OCR），拿到入手行的物名与数量，按游戏内固定的卖店价算出本次收益，按角色与潜艇归档，并给出日均、月均、年均等统计。

当前正式支持 **Windows 10/11 x64**。

> 本项目是非官方社区工具，与 Square Enix 不存在隶属、授权、认可或赞助关系。

## 当前状态

功能已闭环，可以日常用：收艇 → 识别 → 核对 → 入账 → 统计。

**记账**。「收艇记账」页有四种取图方式（拖入、`Ctrl + V`、读剪贴板、选文件），也可以**直接从游戏聊天框读文本**——点「直接读聊天框」读一次，或开「自动监听」每 3 秒轮询、收完艇自动出账。这条路不经过截图与 OCR，不会认错字，也不会漏掉 HQ 标记。

**识别**。截图走「日志文本行 → 日文 OCR → 行模板 → 词表纠错」，认的是聊天记录里的入手行，**不是图标**（早期那套网格切分认图标的路线已整体退役，代码已删）。识别结果逐行列出，可改数量、切 HQ、在歧义时选候选、给词表外物名手填单价，合计实时刷新。

**按艇记账**。层级是 角色 → 艇 → 记录。「潜水艦「X」の探索完了」是归属凭据，一次读取里有几艘艇就写几笔记录；识别把相邻两艘艇串了时，每行的艇归属可以手改。

**统计**。「收益统计」页有总收益 / 日均 / 月均 / 年均四张卡、日/月可切的收益走势图、角色与艇与道具三种占比、已入账记录列表（可逐条删除）、导出 CSV。

**设置**。显示数据目录与数据库位置、一键打开目录、JSON 备份导出与导入（导入整体替换，替换前自动备份）、显示语言（简体中文 / 日本語）、更新检查与网络代理。

**显示语言与收集语言分开**：界面与道具名跟着设置里的语言走，日志用哪种语言写都能照常识别。加一门语言只要放一个 `language-<名>.json`，不用改代码。

## 设计要点

- **离线**：潜艇产出全部卖店，价格由游戏内固定值决定，因此不查市场价、不联网取价、无价格缓存。HQ 物品卖店价固定高于 NQ（向上取整），价格表里只需存一份 NQ 价。
- **识别文字，再查词表**：日志把入手了什么、几个都写清楚了，而潜艇掉落物名是封闭的小集合。因此识别只需在词表里找出最像的那一条，OCR 不必完美，靠词表纠错与人工确认兜底。
- **显示语言与收集语言分开**：物名按 `ItemId` 绑定多语言写法，界面按设置显示；识别则用日志本身的语言（行模板决定），两者互不牵制。
- **不假设截图对齐**：不要求固定窗口尺寸或固定截取范围，行位置从图像内容里算。
- **日志窗口没有底板**：文字直接压在游戏画面上，二值化必须按行带自适应——实测背景亮度能从 8 跳到 180。
- **不触碰游戏**：不注入进程、不写游戏内存、不自动操作游戏、不解析游戏 DAT 文件。可选地从聊天框直接读文本（进程外只读），能避开 OCR 误识；权限要求与游戏同级，游戏不提权就不需要提权。
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
src/FFXIVSubmarineLedger.Domain            角色、部队、收益记录、道具、卖店价等领域模型
src/FFXIVSubmarineLedger.Application       用例与仓储接口
src/FFXIVSubmarineLedger.Infrastructure    SQLite 持久化、文本行解析（`Parsing`）、掉落表（`Catalog` + `Data/items.json`，多语言物名按 ItemId 绑定）
src/FFXIVSubmarineLedger.Platform.Windows  Windows 数据目录
src/FFXIVSubmarineLedger.Desktop           Avalonia UI、MVVM、随包分发的 OCR 模型（`Data/tessdata`）
tests/                               自动化测试
tools/                               日志截图版式量测、窗口截图与剪贴板脚本（Python）
docs/PLAN.md                         技术方案
```

`Domain`、`Application` 与通用 `Infrastructure` 不依赖 Windows 专有 API，平台相关代码集中在 `Platform.Windows`。

## 构建

需要 .NET 10 SDK。

```powershell
dotnet build FFXIVSubmarineLedger.sln
dotnet test FFXIVSubmarineLedger.sln
dotnet run --project src/FFXIVSubmarineLedger.Desktop
```

打一个可分发的包：

```powershell
powershell -File tools/publish.ps1
```

产物是 `artifacts/FFXIVSubmarineLedger-<版本>.zip` 与同名 `.sha256`。自包含发布，目标机器不需要装 .NET。

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
FFXIVSubmarineLedger.exe --image "D:\shots\log.png"
```

## 识别路线

`Infrastructure/Recognition/` 放纯算法部分，不依赖 UI，可以单独测。流程是「日志截图 → 文本行 → 日文 OCR → 行模板 → 词表纠错」：

| 模块 | 职责 | 状态 |
| --- | --- | --- |
| 文本行定位 | `TextLineLocator`：行投影成带 → 按行高中位数滤掉边缘亮边 → 逐行算墨迹左右边界 | 已完成 |
| 日文 OCR | `LinePreprocessor`（裁行、放大 3×、Otsu 二值化、反相）+ `TesseractLineReader`；模型随包放在 `Data/tessdata/` | 已完成 |
| 行模板解析 | `JapaneseLogLineParser`：从 `[HH:MM] 角色名は ▶ 物名×Nを入手した。` 取物名与数量，容忍 OCR 把符号读歪 | 已完成 |
| 词表纠错 | `ItemMatcher`：封闭词表内编辑距离，多候选显式标出并按价降序默认 | 已完成 |
| 聊天框直读 | `Sharlayan` 进程外只读聊天缓冲区，跳过截图与 OCR（推荐路径） | 已完成 |
| 网格检测、图标提取与匹配 | v0.4 的旧路线，认图标不认文字，已整体退役 | 已删 |

日志行格式已用真实样本核实，具体形态、版式参数与遗留未知项见 [docs/PLAN.md](docs/PLAN.md) 第 2 节与 6.4。

量版式参数用：

```bash
python tools/measure_log_lines.py <日志截图.png>
```

不开窗口直接看一张图切出了哪些行：

```bash
FFXIVSubmarineLedger.exe --dump-lines <图片> <输出.txt>
```

输出行带位置、行距、行高与左边距。纯标准库实现，不依赖 Pillow 与 numpy。

另外三个开发命令，用来核对识别链的中间结果（都是写文本文件、不开窗口）：

```bash
FFXIVSubmarineLedger.exe --dump-ocr <图片> <输出.txt> [tessdata 目录]   # 逐行 OCR 文本 + 词表匹配结果
FFXIVSubmarineLedger.exe --dump-chat <输出.txt>                        # 读一次聊天框并按艇归属列出入手行
FFXIVSubmarineLedger.exe --dump-chat-diag <输出.txt>                   # 聊天码分布与每行的解析判定，排查用
```

## 数据位置

```text
%LocalAppData%\FFXIVSubmarineLedger\subledger.db            数据库
%LocalAppData%\FFXIVSubmarineLedger\backups\                导入前的自动备份
```

设置页可以直接打开这个目录，也能把全部数据导出成一个可读的 JSON 文件。导入是整体替换，替换前会自动存一份当前数据。

程序早先叫 `FFXIVSubLedger`，数据放在 `%LocalAppData%\FFXIVSubLedger\`。改名后第一次启动会把这个目录整体搬到新位置；如果两个目录都在，程序不会动它们，交给你自己确认。

## 开源许可证

除明确标注的第三方内容外，本项目的原创源代码与文档采用 [GNU General Public License v3.0 only](https://www.gnu.org/licenses/gpl-3.0.html)。

第三方组件：

- [Tesseract](https://github.com/tesseract-ocr/tesseract)（Apache-2.0），通过 NuGet 包 `Tesseract` 引入。
- [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) 的日文模型 `jpn.traineddata`（Apache-2.0），位于 `src/FFXIVSubmarineLedger.Desktop/Data/tessdata/`。
- [Sharlayan](https://github.com/FFXIVAPP/sharlayan)（MIT），用于进程外读取游戏聊天框文本；它内置 FFXIVClientStructs（MIT）与 Lumina（MIT）。

OCR 引擎与模型都是随程序分发的，不依赖用户系统里装没装 OCR 组件或日文语言包。

## FINAL FANTASY XIV 与 Square Enix 声明

FINAL FANTASY XIV 及相关名称、商标、图像和其他游戏资源的权利归其各自权利人所有。

> FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
>
> © SQUARE ENIX
