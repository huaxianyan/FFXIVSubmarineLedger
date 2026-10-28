using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Catalog;

namespace FFXIVSubmarineLedger.Infrastructure.Parsing;

/// <summary>一条已认出的入手记录。</summary>
/// <param name="LineNumber">日志里第几行，从 1 开始，用于和界面上的行框对上。</param>
/// <param name="ItemText">OCR 读出的物名原文，词表没命中时它就是唯一线索。</param>
/// <param name="Item">词表命中的条目，没命中时为 null。</param>
/// <param name="Candidates">同样接近的词表条目。多于一条时要人确认，不能静默按第一个算钱。</param>
/// <param name="ItemName">采用的道具名。命中时用词表里的写法，没命中时用原串。</param>
/// <param name="ItemId">道具 ID，没命中时为 0。</param>
/// <param name="Quantity">数量。</param>
/// <param name="IsHighQuality">是否 HQ。乘以 HQ 价的依据。</param>
/// <param name="UnitPrice">单位卖店价。没命中时为 0，等用户补。</param>
/// <param name="SubmarineName">这批收益属于哪艘艇。日志里没等到「探索完了」行为 null。</param>
/// <param name="RawLine">产生这条记录的原始日志行，用于去重与排查。</param>
public sealed record RecognizedLoot(
    int LineNumber,
    string ItemText,
    LootCatalogEntry? Item,
    IReadOnlyList<LootCatalogEntry> Candidates,
    string ItemName,
    uint ItemId,
    int Quantity,
    bool IsHighQuality,
    int UnitPrice,
    string? SubmarineName,
    string RawLine)
{
    public bool IsKnown => Item is not null;

    /// <summary>词表里有不止一个同样接近的物名，价钱需要人来定。</summary>
    public bool IsAmbiguous => Candidates.Count > 1;

    public int Subtotal => Quantity * UnitPrice;
}

/// <summary>
/// 把一批 OCR 出来的行变成入手记录：解析行结构 → 对词表 → 算单价。
/// 不认识的行直接跳过，不报错（探索完了、修理、出港都在这条路上被滤掉）。
/// </summary>
public static class LootScanner
{
    /// <param name="includeUnattributed">
    /// 是否把「等不到艇名」的入手行也交给调用方。一次性读取给 true（历史里可能真缺「探索完了」行）；
    /// 监听轮询给 false——那些行多半只是还没等到归属，下一轮就能拿到正确的艇，早结算反而会记错艇。
    /// </param>
    public static IReadOnlyList<RecognizedLoot> Scan(
        IReadOnlyList<string> lines,
        ItemCatalog catalog,
        bool includeUnattributed = true)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(catalog);

        var results = new List<RecognizedLoot>();

        // 日志里艇名在收益行之后，所以先把入手行攒着，遇到「探索完了」再一起结算归属。
        var pending = new List<(int LineNumber, LootLine Parsed)>();

        for (int index = 0; index < lines.Count; index++)
        {
            if (JapaneseLogLineParser.TryParseSubmarineReturn(lines[index], out string submarineName))
            {
                Flush(pending, submarineName, catalog, results);
                pending.Clear();
                continue;
            }

            if (JapaneseLogLineParser.TryParse(lines[index], out LootLine parsed))
                pending.Add((index + 1, parsed));
        }

        // 整段读完还有没等到归属的：一次性读取照样给出来（归属留空），
        // 监听轮询则压着不给，等下一轮「探索完了」行到了再算。
        if (includeUnattributed)
            Flush(pending, submarineName: null, catalog, results);

        return results;
    }

    private static void Flush(
        List<(int LineNumber, LootLine Parsed)> pending,
        string? submarineName,
        ItemCatalog catalog,
        List<RecognizedLoot> results)
    {
        foreach ((int lineNumber, LootLine parsed) in pending)
        {
            ItemMatch match = ItemMatcher.Match(parsed.ItemText, catalog.Entries);

            if (match.Item is not { } item)
            {
                results.Add(new RecognizedLoot(
                    lineNumber,
                    parsed.ItemText,
                    null,
                    [],
                    parsed.ItemText,
                    0,
                    parsed.Quantity,
                    false,
                    0,
                    submarineName,
                    parsed.RawLine));
                continue;
            }

            // 词表说这个道具没有 HQ 版本，那数量前的标记只能是 OCR 把符号读歪了。
            bool highQuality = parsed.LooksHighQuality && item.CanBeHq;

            results.Add(new RecognizedLoot(
                lineNumber,
                parsed.ItemText,
                item,
                match.Candidates,
                item.CanonicalName,
                item.ItemId,
                parsed.Quantity,
                highQuality,
                ShopPrice.For(item.ShopPriceNq, highQuality),
                submarineName,
                parsed.RawLine));
        }
    }
}
