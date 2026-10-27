using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Application.Records;

/// <summary>
/// 一条待入账的道具明细。数量与品质都可能被用户改过，单价是当时采用的卖店价。
/// </summary>
/// <param name="ItemId">道具 ID。词表里没有的物名给 0。</param>
/// <param name="ItemName">道具名。词表命中时用词表写法。</param>
/// <param name="Quantity">数量。</param>
/// <param name="IsHighQuality">是否 HQ。</param>
/// <param name="UnitPrice">单价。未知道具由用户填，没填就是 0。</param>
/// <param name="IsUnknown">是否是词表里没有的物名。</param>
public sealed record LootEntryInput(
    uint ItemId,
    string ItemName,
    int Quantity,
    bool IsHighQuality,
    int UnitPrice,
    bool IsUnknown);

/// <summary>
/// 把确认过的明细组装成一笔收益记录。
///
/// 合并规则是「`ItemId` + 品质」：同一次收艇里同名同品质的道具会分成多行日志，
/// 但账上应该是一条（数量相加）。品质必须参与合并键——HQ 与 NQ 单价不同，
/// 合起来就反推不出钱数。词表外的物名没有 `ItemId`，改用物名做键。
/// </summary>
public static class LootRecordFactory
{
    public static LootRecord Build(
        Guid characterId,
        DateOnly date,
        IReadOnlyList<LootEntryInput> entries,
        string note = "")
    {
        ArgumentNullException.ThrowIfNull(entries);

        var merged = new Dictionary<string, LootItem>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (LootEntryInput entry in entries)
        {
            if (entry.Quantity <= 0)
                continue;

            string key = MergeKey(entry);

            if (merged.TryGetValue(key, out LootItem? existing))
            {
                existing.Quantity += entry.Quantity;
                continue;
            }

            merged[key] = new LootItem
            {
                ItemId = entry.ItemId,
                ItemName = entry.ItemName,
                Quantity = entry.Quantity,
                IsHighQuality = entry.IsHighQuality,
                UnitPrice = entry.UnitPrice,
                IsUnknown = entry.IsUnknown,
            };

            order.Add(key);
        }

        return new LootRecord
        {
            CharacterId = characterId,
            Date = date,
            Note = note,
            Items = [.. order.Select(key => merged[key])],
        };
    }

    private static string MergeKey(LootEntryInput entry)
    {
        string identity = entry.IsUnknown
            ? $"u:{entry.ItemName}"
            : $"i:{entry.ItemId}";

        return entry.IsHighQuality ? identity + ":hq" : identity;
    }
}
