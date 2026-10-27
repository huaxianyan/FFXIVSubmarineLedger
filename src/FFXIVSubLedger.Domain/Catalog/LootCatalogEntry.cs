namespace FFXIVSubLedger.Domain.Catalog;

/// <summary>
/// 潜艇掉落候选表的一行：道具、日文名、NQ 卖店价、能否 HQ。
/// 这张表既是识别时的匹配候选集，也是计价时的查表依据。卖店价只存 NQ，
/// HQ 价由 <see cref="ShopPrice"/> 换算。
/// </summary>
/// <param name="ItemId">游戏内的道具 ID，跨语言唯一。</param>
/// <param name="Name">日文物名，日语客户端的日志里就是这一串。</param>
/// <param name="ShopPriceNq">NQ 卖店价。</param>
/// <param name="CanBeHq">该道具是否存在 HQ 版本。</param>
public sealed record LootCatalogEntry(uint ItemId, string Name, int ShopPriceNq, bool CanBeHq);
