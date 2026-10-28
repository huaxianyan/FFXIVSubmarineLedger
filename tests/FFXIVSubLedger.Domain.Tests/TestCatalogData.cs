using FFXIVSubLedger.Domain.Catalog;

namespace FFXIVSubLedger.Domain.Tests;

/// <summary>测试用的掉落表构造。物名在多语言表里是键值对，测试只关心一个写法。</summary>
internal static class TestCatalogData
{
    public static LootCatalogEntry Item(uint itemId, string name, int shopPriceNq, bool canBeHq = false) => new(
        itemId,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["ja"] = name, ["zh-Hans"] = name },
        shopPriceNq,
        canBeHq);
}
