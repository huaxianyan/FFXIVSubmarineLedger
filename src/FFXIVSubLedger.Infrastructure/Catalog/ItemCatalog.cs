using System.Text.Json;
using FFXIVSubLedger.Domain.Catalog;

namespace FFXIVSubLedger.Infrastructure.Catalog;

/// <summary>
/// 掉落候选表。数据来自游戏数据（XIVAPI 的 <c>Item.PriceLow</c> = 卖店价、<c>CanBeHq</c>），
/// 以可读 JSON 随程序分发，用户能自己看、自己补。
/// </summary>
public sealed class ItemCatalog
{
    private readonly Dictionary<uint, LootCatalogEntry> _byId;

    public ItemCatalog(IReadOnlyList<LootCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = entries;
        _byId = [];

        foreach (LootCatalogEntry entry in entries)
        {
            if (!_byId.TryAdd(entry.ItemId, entry))
                throw new InvalidDataException($"掉落表里 ItemId {entry.ItemId} 重复了。");
        }
    }

    public IReadOnlyList<LootCatalogEntry> Entries { get; }

    /// <summary>默认位置：程序目录下的 <c>Data/items.ja.json</c>。</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "items.ja.json");

    public static ItemCatalog Load(string? path = null)
    {
        string resolved = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;

        if (!File.Exists(resolved))
            throw new FileNotFoundException($"找不到掉落表：{resolved}", resolved);

        string json = File.ReadAllText(resolved);
        ItemEntry[]? rows = JsonSerializer.Deserialize<ItemEntry[]>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip });

        if (rows is null || rows.Length == 0)
            throw new InvalidDataException($"掉落表是空的：{resolved}");

        return new ItemCatalog([.. rows.Select(x => new LootCatalogEntry(x.ItemId, x.Name, x.ShopPriceNq, x.CanBeHq))]);
    }

    public LootCatalogEntry? Find(uint itemId) => _byId.GetValueOrDefault(itemId);

    private sealed record ItemEntry(uint ItemId, string Name, int ShopPriceNq, bool CanBeHq);
}
