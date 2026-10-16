using FFXIVSubLedger.Domain.Catalog;

namespace FFXIVSubLedger.Application.Abstractions;

public interface ILootCatalogRepository
{
    Task<IReadOnlyList<LootCatalogEntry>> ListAsync(CancellationToken ct = default);

    Task<LootCatalogEntry?> FindByItemIdAsync(uint itemId, CancellationToken ct = default);

    /// <summary>同一图标可能对应多个道具，识别时需要拿到全部候选。</summary>
    Task<IReadOnlyList<LootCatalogEntry>> ListByIconIdAsync(uint iconId, CancellationToken ct = default);

    Task UpsertAsync(LootCatalogEntry entry, CancellationToken ct = default);

    Task ReplaceBuiltInAsync(IReadOnlyList<LootCatalogEntry> entries, CancellationToken ct = default);
}
