using System.Globalization;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;
using FFXIVSubmarineLedger.Platform.Windows.GameChat;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 开发期命令：从游戏聊天框读一遍内容写成文本，用来核对真实行格式。
///
/// 用法：`FFXIVSubmarineLedger.exe --dump-chat &lt;输出.txt&gt;`（权限需与游戏同级）。
/// </summary>
internal static class DumpChatCommand
{
    public static bool TryParse(string[] args, out string outputPath)
    {
        outputPath = string.Empty;

        if (args.Length < 2 || args[0] is not "--dump-chat")
            return false;

        outputPath = args[1];
        return true;
    }

    public static void Run(string outputPath)
    {
        var text = new System.Text.StringBuilder();

        using var reader = new SharlayanChatReader();

        if (!reader.TryAttach(out string message))
        {
            text.AppendLine(message);
            File.WriteAllText(outputPath, text.ToString());
            return;
        }

        IReadOnlyList<string> lines = reader.ReadNewLines();

        // 紧接着再读一次：用来确认读取器给的是「增量」还是「把整段历史重放一遍」。
        // 增量则第二轮应接近 0；重放则第二轮与第一轮几乎一样多。
        Thread.Sleep(1000);
        IReadOnlyList<string> second = reader.ReadNewLines();

        text.AppendLine(CultureInfo.InvariantCulture, $"已连接 {reader.IsAttached}");
        text.AppendLine(CultureInfo.InvariantCulture, $"首轮 {lines.Count} 行 / 第二轮 {second.Count} 行");

        foreach (string line in lines)
            text.AppendLine(line);

        // 顺手把认出的入手行也列上，一条命令核完整条链。
        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(lines, ItemCatalog.Load());
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"入手 {loot.Count} 条");

        foreach (RecognizedLoot entry in loot)
        {
            string name = entry.IsHighQuality ? $"{entry.ItemName}(HQ)" : entry.ItemName;
            string submarine = entry.SubmarineName is { Length: > 0 } sub ? $"[{sub}] " : "[未标明艇] ";
            string suffix = entry.IsKnown ? string.Empty : "  [未命中词表]";
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"#{entry.LineNumber:00} {submarine}{name} ×{entry.Quantity} @{entry.UnitPrice} = {entry.Subtotal}{suffix}");
        }

        File.WriteAllText(outputPath, text.ToString());
    }
}
