using System.Text.Json;
using FFXIVSubLedger.Domain.Catalog;

namespace FFXIVSubLedger.Infrastructure.Catalog;

/// <summary>
/// 掉落候选表。价格与「能否 HQ」来自 XIVAPI（<c>PriceLow</c> / <c>CanBeHq</c>），
/// 各语言物名用 ItemId 绑定：日文名来自 XIVAPI，简体中文名来自 ffxiv-datamining-cn 的 <c>Item.csv</c>。
/// 以可读 JSON 随程序分发，用户能自己看、自己补；加一门语言只是往 <c>names</c> 里加一个键。
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

    /// <summary>默认位置：程序目录下的 <c>Data/items.json</c>。</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", "items.json");

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

        foreach (ItemEntry row in rows.Where(x => x.Names is null || x.Names.Count == 0))
            throw new InvalidDataException($"掉落表里 ItemId {row.ItemId} 没有物名：{resolved}");

        return new ItemCatalog(
        [
            .. rows.Select(x => new LootCatalogEntry(
                x.ItemId,
                new Dictionary<string, string>(x.Names, StringComparer.OrdinalIgnoreCase),
                x.ShopPriceNq,
                x.CanBeHq)),
        ]);
    }

    public LootCatalogEntry? Find(uint itemId) => _byId.GetValueOrDefault(itemId);

    private sealed record ItemEntry(
        uint ItemId,
        Dictionary<string, string> Names,
        int ShopPriceNq,
        bool CanBeHq);
}
