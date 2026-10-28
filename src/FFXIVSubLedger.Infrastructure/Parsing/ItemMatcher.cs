using FFXIVSubLedger.Domain.Catalog;

namespace FFXIVSubLedger.Infrastructure.Parsing;

/// <summary>物名与词表比对的结果。</summary>
/// <param name="Item">命中的条目，没命中时为 null。</param>
/// <param name="Distance">编辑距离，0 表示完全一致。</param>
/// <param name="Candidates">同样接近的其它条目，供确认界面让人选。</param>
public sealed record ItemMatch(LootCatalogEntry? Item, int Distance, IReadOnlyList<LootCatalogEntry> Candidates)
{
    public bool IsExact => Item is not null && Distance == 0;

    public bool IsAmbiguous => Candidates.Count > 1;
}

/// <summary>
/// 把 OCR 读出的物名对到词表上。
///
/// 词表只有几十条，逐条算编辑距离就够快。判据是「够不够近」，不是「哪条最近」——
/// 最近但离得很远的匹配宁可判为未命中，让人来选。
/// </summary>
public static class ItemMatcher
{
    /// <summary>允许的最大编辑距离。物名普遍 8–12 字，超过 2 处差异基本不是同一个道具。</summary>
    public const int MaxDistance = 2;

    public static ItemMatch Match(string itemText, IReadOnlyList<LootCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(itemText);
        ArgumentNullException.ThrowIfNull(entries);

        string normalized = Normalize(itemText);
        if (normalized.Length == 0)
            return new ItemMatch(null, int.MaxValue, []);

        int limit = Math.Min(MaxDistance, Math.Max(1, normalized.Length / 4));
        int best = int.MaxValue;
        var candidates = new List<LootCatalogEntry>();

        foreach (LootCatalogEntry entry in entries)
        {
            // 收集时日志可能是任一语言，所以在该道具的所有写法里取最近的那个。
            int distance = entry.AllNames.Min(name => Levenshtein(normalized, Normalize(name)));
            if (distance > limit)
                continue;

            if (distance < best)
            {
                best = distance;
                candidates.Clear();
                candidates.Add(entry);
            }
            else if (distance == best)
            {
                candidates.Add(entry);
            }
        }

        if (candidates.Count == 0)
            return new ItemMatch(null, int.MaxValue, []);

        // 距离一样时就按卖店价降序排：确认界面默认选第一个，贵的那个更可能是真的。
        if (candidates.Count > 1)
        {
            candidates.Sort((left, right) =>
            {
                int byPrice = right.ShopPriceNq.CompareTo(left.ShopPriceNq);
                return byPrice != 0 ? byPrice : left.ItemId.CompareTo(right.ItemId);
            });
        }

        return new ItemMatch(candidates[0], best, candidates);
    }

    private static string Normalize(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);

        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>编辑距离（插入、删除、替换各计 1）。</summary>
    private static int Levenshtein(string left, string right)
    {
        if (left.Length == 0)
            return right.Length;

        if (right.Length == 0)
            return left.Length;

        // 物名只有几十条、每条一二十字，直接用数组，不玩缓冲区复用。
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
