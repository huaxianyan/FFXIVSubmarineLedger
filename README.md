# FFXIVSubLedger

FF14 潜艇收益统计工具。收艇后截取背包区域，自动识别道具图标，按游戏内固定的卖店价算出本次收益，归档到角色/部队，并提供日均、月均、年均等统计。

当前正式支持 **Windows 10/11 x64**。

> 本项目是非官方社区工具，与 Square Enix 不存在隶属、授权、认可或赞助关系。

## 当前状态

**M1 骨架已完成**：解决方案分层、SQLite 持久化、多角色管理界面，界面框架对齐 FFXIVConfigManager。

**M2 截图入口 + 网格检测已完成**：

- 拖入、Ctrl+V、读剪贴板、选图片文件四种方式都能把截图收进来并预览，显示尺寸、来源与收益归属角色。
- 网格自动检测：边缘能量投影 → 自相关求周期 → 相位搜索对齐，配合「正方形先验」解决可见行数太少的问题。切分结果叠加回预览图（竖线红、横线青，空位压暗），供人工核对。
- 实测样本（371 × 177）稳定检出 `格子边长 69 px · 5 列 × 2 行 · 7 格有道具 / 3 格空位`，两个尺度（原始与 2× 放大）结果一致。

图标匹配、数量识别与统计功能尚未实现。完整方案与里程碑见 [docs/PLAN.md](docs/PLAN.md)。

## 设计要点

- **离线**：潜艇产出全部卖店，价格由游戏内固定值决定，因此不查市场价、不联网取价、无价格缓存。
- **识别图标而非文字**：截图里只有道具图标，图标是游戏固定素材，跨服跨语言一致。
- **网格自适应**：人工截图边界不精准，网格从图像内容自行推算，不按截图边框等分。
- **不触碰游戏**：不注入进程、不读写游戏内存、不解析游戏 DAT 文件。
- **多种取图方式**：不强制用户先存成文件，剪贴板与拖放是主路径。

## 技术栈

- C# / .NET 10 LTS
- Avalonia UI 12 + CommunityToolkit.Mvvm
- SQLite（Microsoft.Data.Sqlite）
- xUnit

## 目录结构

```text
src/FFXIVSubLedger.Domain            角色、部队、收益记录、道具、卖店价等领域模型
src/FFXIVSubLedger.Application       用例与仓储接口
src/FFXIVSubLedger.Infrastructure    SQLite 持久化
src/FFXIVSubLedger.Platform.Windows  Windows 数据目录
src/FFXIVSubLedger.Desktop           Avalonia UI、MVVM
tests/                               自动化测试
tools/                               网格检测预研脚本、窗口截图与剪贴板脚本（Python）
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

## 网格检测

`Infrastructure/Recognition/` 下是纯算法部分，不依赖 UI，可以单独测：

| 文件 | 职责 |
| --- | --- |
| `GrayImage.cs` | 灰度图容器 |
| `GridDetector.cs` | 边缘能量投影 → 自相关求周期 → 相位搜索对齐网格线 → 切分格位并判空 |
| `GridDetectionOptions.cs` | 阈值参数（最小格边长、空格子标准差阈值、格位向内收缩比例等） |
| `GridDetectionResult.cs` | 检测结果：边长、网格线、相关度/锐度、每个格位的统计与空位标记 |
| `GridOverlay.cs` | 把网格线画到 BGRA 缓冲区上，供预览核对 |

只从列方向定周期，行方向沿用同一周期单独搜相位（**正方形先验**）——收艇截图常见只有 2 行，行向自相关不可靠（多尺度实测列向 7/7 命中、行向仅 3/7）。

`tests/.../Recognition/RealBagSampleTests.cs` 用一张真实截图（`Samples/bag-371x177.pgm`）做回归。这个样本是必要的：合成图测不出「截图贴着最后一格右边缘裁」这类真实约束，而它恰好是一个曾经漏掉整列道具的 bug。

## 数据位置

```text
%LocalAppData%\FFXIVSubLedger\subledger.db
```

## 开源许可证

除明确标注的第三方内容外，本项目的原创源代码与文档采用 [GNU General Public License v3.0 only](https://www.gnu.org/licenses/gpl-3.0.html)。

## FINAL FANTASY XIV 与 Square Enix 声明

FINAL FANTASY XIV 及相关名称、商标、图像和其他游戏资源的权利归其各自权利人所有。

> FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
>
> © SQUARE ENIX
