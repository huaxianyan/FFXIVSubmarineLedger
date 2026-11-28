# 进度

## 当前主线

v0.1.4 已发布（2026-11-28），收益按日志里的角色名归属、角色页可粘贴与整卡编辑艇名。识别、计价、备份、更新检查与多语言都可日常用。技术方案与后续事项在 `PLAN.md`（第 11 节是下一步），工程细则在 `ENGINEERING.md`，发布流程在 `../RELEASE.md`。

近期工作重心：等真实收艇样本反馈，按「未命中」与「未标明艇」两类问题补词表和归属判定。

## STATUS-011：名字匹配不再区分大小写（2026-11-28）

- 用户问「角色首字母没大写会有匹配问题吗」，查下来确实有：`NameMatcher.Normalize` 只抹空格、比较走 `StringComparison.Ordinal`。手填 `tiina branford` 与游戏里的 `Tiina Branford` 差一个字，只能靠模糊匹配置信度捡回来并每次提示「读成…」；填成全大写就差 13 个字，直接判成「没见过的名字」——整批收益认不出角色，艇也会被当成新的再建一艘。
- 修法：比对键 `NameMatcher.NormalizeKey` = 去空格 + `ToLowerInvariant`，`Guess` 的精确判定与编辑距离都走它（返回的仍是库里那个原始大小写）。`RosterEditor` 的角色重名、艇名去重也用它，`Blackberry` 与 `blackberry` / `Black berry` 不能共存。另加一条防御：界面传了不完整的艇列表时，新填的名字不能撞上没出现在卡上的已有艇。
- 测试：`NameMatcherTests.LetterCaseAndSpacingDoNotMakeANewName`（4 组，先红后绿）、`RosterEditorTests.TreatsLetterCaseAndSpacesAsTheSameName`、`TreatsLetterCaseAsTheSameCharacter`。Domain 194 + Desktop 31 全过。

## STATUS-010：发布 v0.1.4（2026-11-28）

- 版本号 `Directory.Build.props` → `0.1.4`，发布说明 `docs/release-notes/v0.1.4.md`（新功能 5 条、问题修复 2 条，25 行，含 `blob/v0.1.4/` 链接）。
- 产物 `artifacts/FFXIVSubmarineLedger-0.1.4.zip`，61,823,127 B，SHA-256 `385cb3d0…9925`；zip + sha256 两个附件，无 label，标题只有版本号。
- 打标签前把 zip 解压到 `artifacts/run` 启动了两次：窗口标题出来、`startup.log` 到「数据库就绪」、进程按 PID 关掉。
- 线上逐项核对：`/releases/latest` = `v0.1.4`、资产名与大小对得上、下载实算 SHA-256 与校验文件、本地产物、发布说明三处一致、正文与本地文件逐字符相同（只多一个 gh 自动加的结尾空行）。

## STATUS-009：按行归属角色，去掉默认角色；角色页可粘贴、可编辑、可给艇改名（2026-11-28）

- **默认角色整条撤掉**（用户提出）：收艇日志本身就写着是谁得的，再让人指定一遍既多余又容易配错——配错了整批收益会记到别人头上。改动面：`Character.IsDefault`、`CharacterService.SetDefaultAsync`（及停用/删除时的转移逻辑）、`AppSettings.DefaultCharacterId` / `LastSelectedCharacterId`、备份里的 `IsDefault`、角色页的默认标记，全部删除；DB 的 `is_default` 列留着不动（SQLite 删列要重建表，不值得），代码不再读写。
- **多角色各记各的**（用户提问的确认）：`JapaneseLogLineParser` 现在把「<角色名>は」里的角色名带出来（`LootLine.ActorName` → `RecognizedLoot.CharacterName`），落库按「角色 + 艇」分组，一组一笔记录。`LootRecordFactory` 不变。
- **归属解析抽成 `OwnershipResolver`**（Application）：角色名与艇名都过 `NameMatcher` 的两档阈值（原来的 `SubmarineNameMatcher` 泛化成 `Application/Names/NameMatcher`）。角色名读歪一个字会改写并标出来；没登记过、同名不同服、分不清的一律置空，行内下拉让人选；**有任一行没选角色就不许入账**。
- **角色页**：`Ctrl + V` 粘贴（`角色名@服务器` 自动拆两个字段；添加区那两个框里也接管，改角色名/艇名的输入框里留给 TextBox 自己粘）。**没加粘贴按钮**（用户要求只留快捷键）。名下艇名**横排**摆在卡片里（原先折叠成「潜水艦 N 艘」——中文界面用了日文词，用户指出后取消），点「编辑」时角色名、服务器、每艘艇名一起变成输入框，由 `RosterEditor.SaveAsync` **一次性校验、一次性写入**：任一名字不合法就整张卡退回去，一个字节都不写。艇改名 ID 不变，历史记录不分裂。
- **新角色也能先填艇名**（用户反馈「新建角色不能填潜艇、新角色编辑也没有」）：添加区与编辑态都固定 4 个位置，占位「艇名 N（可留空）」；空位跳过，填了就在保存时新建，已有艇被清空名字则报错。`RosterSubmarine.Id` 为空即「新填」，`RosterEditor` 先校验全部再一次写入。
- 新增测试：`OwnershipResolverTests`（8）、`SubmarineServiceTests`（4）、`CharacterPasteTests`（6）、解析器的角色名用例（4）、`NameMatcherTests` 改名沿用。Domain 184 + Desktop 31 全过；`RenderProbe` 加了角色页，重新渲染核对了记账页的「角色 + 艇」双下拉与摘要。

## STATUS-001：正式发布 v0.1.0

- 首个正式版发布，zip + sha256 两个附件，自包含目录包约 54 MB。
- 应用内更新检查从此不再回 404。
- 产物命名与发布说明按 `release-notes-standard` 对齐，后续版本照此续。

## STATUS-002：图标定为用户的纪念币方案

- 源图是用户提供的金色浮雕潜艇纪念币，工具链在 `tools/make_icon.py`。
- 抠底、正圆化、补对比、压高光四件事的细节与顺序见 `ENGINEERING.md` 的「图标流水线」。

## STATUS-008：发布 v0.1.3（全新安装起不来）

- 用户 2026-11-28 反馈：装完第一次启动就弹「no such table: settings」，反复重启都一样。
- 根因：`ApplicationComposition` 构造里读设置定界面语言（要查 `settings` 表），而建表排在它后面——空库上必然失败。开发机库早就建好，所以 v0.1.0 起的三个版本一直没露出来。
- 改法：组合根构造的第一件事就是 `database.Initialize()`（幂等），启动流程里重复的调用去掉；新增 `Desktop.Tests/FreshInstallTests` 两条用例（空目录可装配、同目录可装配两次）。**已验证去掉修法后这两条必挂。**
- 影响面：全新安装的用户；已有库的老用户不受影响。之前启动失败留下的空库会被自动建表，不用手动删。

## STATUS-007：发布 v0.1.2（艇名误读不再造艇）

- 版本号 0.1.1 → 0.1.2，只修缺陷；产物 `FFXIVSubmarineLedger-0.1.2.zip` + `.sha256`。
- 发布前 `dotnet build` / `dotnet test` 全绿（Domain 171 + Desktop 23），无头渲染确认确认行的艇下拉与提示排得下。
- 用户库里的三艘空壳艇已在发布前手动归并（见 STATUS-006），备份 `subledger.db.bak-20261123095537`。

## STATUS-006：艇名读歪会造出新艇

- 用户 2026-11-23 发现：前一天那张截图的艇名被读歪后按字面新建了三艘艇——Blackberry 读成 Blackberrvy、Blueberry 读成 Blug5erry、Strawberry 读成 Strawberrv，按艇占比里多了三个各 1 次的名。
- 原因：归属只做精确查名，查不到就 `AddAsync` 新建；确认行里也没有改归属的入口（文档写了但界面里没有）。
- 改法：新增 `SubmarineNameMatcher`（两档阈值，8 字以下不自动改写），识别完与落库前各刷一次角色名下艇名；确认行补上艇归属下拉（角色名下所有艇 + 未标明）与一行提示。分清不了的置未标明交给人选，不猜也不新建。
- 同一份 DB 里的历史脏数据要手动并回去（12 条记录 + 3 艘空艇），代码修不了已落库的记录。

## STATUS-005：发布 v0.1.1，顺手修掉高危依赖漏洞

- `SQLitePCLRaw` 2.1.11 带 CVE-2025-6965（GHSA-2m69-gcr7-jv3q，高），NuGet 审计把 NU1903 当错误，`dotnet build` 整条命令直接失败（改动前就失败，与识别修复无关）。
- 改法：`Microsoft.Data.Sqlite` 10.0.0 → 10.0.15，`SQLitePCLRaw` 跟着到 2.1.13。不要用 `-p:NuGetAudit=false` 绕，下个人拉下来又卡一次。
- 版本号 0.1.0 → 0.1.1（只修缺陷，不改可见能力），产物 `FFXIVSubmarineLedger-0.1.1.zip` + `.sha256`。
- 发布前实测：`dotnet build` / `dotnet test` 全绿（Domain 162 + Desktop 23），解压出来的包用 `--dump-ocr` 跑用户那张图 16/16 行入账、合计 227,110。

## STATUS-004：修亮背景下的整行丢失与噪声未命中

- 用户提供的 1197 × 877 截图（日志下半压在亮色地板上）：只出 15 行、3 行未命中词表、3 行未标明艇。
- 两个根因：① 亮背景把底部三行粘成一条 89 px 高带子，被行高上限整块丢掉（最后一行收益直接不见了）；② `▶` 读成 `5`/`5p`、`×` 读成 `メ`/`ぶ`，噪声字符剥不干净，物名匹配不上词表。
- 改法：过高的带子按描边在带内重分（不连正常带子一起改，老样本输出零变化）；链接标记按「日文物名以假名/汉字起头」直接跳，不再逐个列；潜艇行只认「水艦「」（底部行第一个字常被读错）。
- 同一张图现在 16/16 行入账，合计 227,110 与手算一致。新增 7 个用例，Domain 162 + Desktop 23 全过。

## STATUS-003：设置页加调试模式

- 打开后把聊天框读到的原文导出成 txt，给其它语言客户端留适配入口。
- 去重按同一行出现的次数算，避免被整段累计历史灌满。

## 归档索引

暂无。条目超过 30 条或 500 行时把已完结的整批切到 `docs/archive/`。
