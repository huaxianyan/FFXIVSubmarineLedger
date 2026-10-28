namespace FFXIVSubLedger.Domain.Catalog;

/// <summary>
/// 潜艇掉落候选表的一行：道具、各语言的物名、NQ 卖店价、能否 HQ。
/// 这张表既是识别时的匹配候选集，也是计价时的查表依据。卖店价只存 NQ，
/// HQ 价由 <see cref="ShopPrice"/> 换算。
/// </summary>
/// <param name="ItemId">游戏内的道具 ID，跨语言唯一，也是各语言物名的绑定键。</param>
/// <param name="Names">语言标签 → 物名。日志用哪种语言写，就按哪种语言匹配。</param>
/// <param name="ShopPriceNq">NQ 卖店价。</param>
/// <param name="CanBeHq">该道具是否存在 HQ 版本。</param>
public sealed record LootCatalogEntry(
    uint ItemId,
    IReadOnlyDictionary<string, string> Names,
    int ShopPriceNq,
    bool CanBeHq)
{
    /// <summary>按语言取名字；缺这门语言时退回任意一种写法。</summary>
    public string NameIn(string language)
        => Names.TryGetValue(language, out string? name) ? name : CanonicalName;

    /// <summary>按当前界面语言取名字，供绑定直接使用。</summary>
    public string DisplayName => NameIn(Domain.Settings.ItemDisplayLanguage.Current);

    /// <summary>表里第一门语言的写法，用于落库时的规范名（显示时按 ItemId 换）。</summary>
    public string CanonicalName => Names.Values.First();

    /// <summary>所有语言的写法。收集时日志可能是任一语言，匹配要在全集里找。</summary>
    public IReadOnlyList<string> AllNames => [.. Names.Values];
}
