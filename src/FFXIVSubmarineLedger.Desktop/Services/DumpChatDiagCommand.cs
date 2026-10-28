using System.Globalization;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;
using FFXIVSubmarineLedger.Platform.Windows.GameChat;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 开发期命令：只导出与收艇相关的聊天行，用于排查「读到了但认不出」。
///
/// 相比 <c>--dump-chat</c>，它不落盘无关聊天内容，只写：
/// 读取器状态、聊天码分布、含关键字的行、以及这些行的解析判定。
///
/// 用法：`FFXIVSubmarineLedger.exe --dump-chat-diag &lt;输出.txt&gt;`。
/// </summary>
internal static class DumpChatDiagCommand
{
    private static readonly string[] Keywords = ["入手", "潜水艦", "沈没船", "海産", "エメラルド", "ルビー", "探索"];

    public static bool TryParse(string[] args, out string outputPath)
    {
        outputPath = string.Empty;

        if (args.Length < 2 || args[0] is not "--dump-chat-diag")
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
            text.AppendLine($"连接失败：{message}");
            File.WriteAllText(outputPath, text.ToString());
            return;
        }

        IReadOnlyList<string> lines = reader.ReadNewLines();

        text.AppendLine(CultureInfo.InvariantCulture, $"行数 {lines.Count}");

        // 聊天码分布：日志行开头的 4 位码，能看出这类消息属于哪一类。
        var codes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            string code = ExtractCode(line);
            codes[code] = codes.GetValueOrDefault(code) + 1;
        }

        text.AppendLine();
        text.AppendLine("聊天码分布：");
        foreach (KeyValuePair<string, int> pair in codes.OrderByDescending(x => x.Value))
            text.AppendLine(CultureInfo.InvariantCulture, $"  {pair.Key} = {pair.Value}");

        text.AppendLine();
        text.AppendLine("含关键字的行与解析判定：");

        int hits = 0;
        foreach (string line in lines)
        {
            if (!Keywords.Any(keyword => line.Contains(keyword, StringComparison.Ordinal)))
                continue;

            hits++;

            string verdict = JapaneseLogLineParser.TryParse(line, out LootLine parsed)
                ? $"入手行 OK 物名「{parsed.ItemText}」×{parsed.Quantity} HQ={parsed.LooksHighQuality}"
                : JapaneseLogLineParser.TryParseSubmarineReturn(line, out string submarine)
                    ? $"探索完了行 艇「{submarine}」"
                    : "未识别（不是入手行也不是探索完了行）";

            text.AppendLine($"  [{ExtractCode(line)}] {line}");
            text.AppendLine($"      → {verdict}");
        }

        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"关键字命中 {hits} 行");

        // 走一遍完整识别链，看最终能算出多少钱、哪些物名不在词表里。
        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(lines, ItemCatalog.Load());
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"识别结果 {loot.Count} 条：");

        foreach (RecognizedLoot entry in loot)
        {
            string submarine = entry.SubmarineName is { Length: > 0 } sub ? sub : "未标明艇";
            string hq = entry.IsHighQuality ? "(HQ)" : string.Empty;
            string unknown = entry.IsKnown ? string.Empty : "  [未命中词表]";

            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"  [{submarine}] {entry.ItemName}{hq} ×{entry.Quantity} @{entry.UnitPrice} = {entry.Subtotal}{unknown}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"合计 {loot.Sum(x => x.Subtotal)}");

        File.WriteAllText(outputPath, text.ToString());
    }

    /// <summary>取行首的聊天码。<c>--dump-chat</c> 的形态是「[HH:MM] 内容」，这里按冒号前四位猜。</summary>
    private static string ExtractCode(string line)
    {
        int end = line.IndexOf(']', StringComparison.Ordinal);
        string body = end >= 0 && end + 2 < line.Length ? line[(end + 2)..] : line;

        int colon = body.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 ? body[..colon] : "(无码)";
    }
}
