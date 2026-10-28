namespace FFXIVSubLedger.Infrastructure.Parsing;

/// <summary>
/// 记录哪些入手行已经落成过记录，避免同一批历史被重复加入。
///
/// 判定键是「艇名 + 原始行」，不是「出现次数」：日志是有序的，归属也按整段有序缓冲算，
/// 所以同一条文本在不同艇下面就是两条不同记录（收艇时同名道具分属两艘艇很常见）。
/// 只按文本计数会把它们并成一条，按次数比又会在一艘艇重复出现同文本时漏计。
/// </summary>
public sealed class LootAccumulator
{
    private readonly Dictionary<string, int> _materialized = new(StringComparer.Ordinal);

    /// <summary>从完整的有序解析结果里挑出还没落成记录的条目。</summary>
    public IReadOnlyList<RecognizedLoot> TakeNew(IReadOnlyList<RecognizedLoot> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var fresh = new List<RecognizedLoot>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (RecognizedLoot entry in entries)
        {
            string key = Key(entry);
            int count = seen.GetValueOrDefault(key) + 1;
            seen[key] = count;

            if (count <= _materialized.GetValueOrDefault(key))
                continue;

            _materialized[key] = count;
            fresh.Add(entry);
        }

        return fresh;
    }

    private static string Key(RecognizedLoot entry) => $"{entry.SubmarineName}\u0001{entry.RawLine}";
}
