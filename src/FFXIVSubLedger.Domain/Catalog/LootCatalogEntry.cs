namespace FFXIVSubLedger.Domain.Catalog;

/// <summary>
/// 潜艇掉落候选表的一行：图标 + 道具名 + 固定卖店价。
/// 这张表既是识别时的匹配候选集，也是计价时的查表依据。
/// </summary>
public sealed record LootCatalogEntry(
    uint ItemId,
    uint IconId,
    string Name,
    int ShopPrice,
    bool IsBuiltIn);
