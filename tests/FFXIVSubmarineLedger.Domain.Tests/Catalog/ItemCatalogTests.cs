using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;

namespace FFXIVSubmarineLedger.Domain.Tests.Catalog;

public sealed class ItemCatalogTests
{
    [Fact]
    public void Load_ReadsTheShippedJapaneseTable()
    {
        ItemCatalog catalog = ItemCatalog.Load();

        // 12 条基础潜水艦掉落 + 12 条宝石类 + 白金锭（直读聊天框时遇到的，截图样本里没有）。
        // 不写死条数：词表会随着新掉落继续加，硬编码只会每次都要改。
        Assert.True(catalog.Entries.Count >= 25, $"词表只有 {catalog.Entries.Count} 条，少了。");

        LootCatalogEntry platinum = Assert.IsType<LootCatalogEntry>(catalog.Find(9360));
        Assert.Equal("プラチナインゴット", platinum.CanonicalName);
        Assert.Equal("白金锭", platinum.NameIn("zh-Hans"));
        Assert.Equal(4, platinum.ShopPriceNq);

        // 聊天框直读会带来截图里没出现过的宝石，词表少一条就会变成「未命中」。
        LootCatalogEntry starRuby = Assert.IsType<LootCatalogEntry>(catalog.Find(12544));
        Assert.Equal("スタールビー", starRuby.CanonicalName);
        Assert.Equal(3, starRuby.ShopPriceNq);

        LootCatalogEntry necklace = Assert.IsType<LootCatalogEntry>(catalog.Find(22507));
        Assert.Equal("沈没船の高級首飾り", necklace.CanonicalName);
        Assert.Equal(34500, necklace.ShopPriceNq);
        Assert.False(necklace.CanBeHq);
    }

    [Fact]
    public void Load_RejectsADuplicatedItemId()
    {
        string path = Path.Combine(Path.GetTempPath(), $"items-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            """
            [
              { "itemId": 1, "names": { "ja": "あ" }, "shopPriceNq": 1, "canBeHq": false },
              { "itemId": 1, "names": { "ja": "い" }, "shopPriceNq": 2, "canBeHq": false }
            ]
            """);

        try
        {
            Assert.Throws<InvalidDataException>(() => ItemCatalog.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Match_PrefersTheOnlyCandidateInsideTheDistanceLimit()
    {
        var entries = new List<LootCatalogEntry>
        {
            TestCatalogData.Item(1, "沈没船の首飾り", 13000, false),
            TestCatalogData.Item(2, "沈没船の高級首飾り", 34500, false),
        };

        ItemMatch match = ItemMatcher.Match("沈没船の高級括飾り", entries);

        Assert.Equal(2u, match.Item?.ItemId);
        Assert.Equal(1, match.Distance);
        Assert.Single(match.Candidates);
    }

    [Fact]
    public void Match_ReportsEveryEquallyCloseCandidate()
    {
        var entries = new List<LootCatalogEntry>
        {
            TestCatalogData.Item(1, "テストアイテム甲", 10, false),
            TestCatalogData.Item(2, "テストアイテム乙", 20, false),
        };

        ItemMatch match = ItemMatcher.Match("テストアイテム丙", entries);

        Assert.True(match.IsAmbiguous);
        Assert.Equal(2, match.Candidates.Count);
    }

    [Fact]
    public void Match_GivesUpWhenNothingIsCloseEnough()
    {
        ItemMatch match = ItemMatcher.Match("まったく別の物", [TestCatalogData.Item(1, "エメラルド", 2, true)]);

        Assert.Null(match.Item);
        Assert.Empty(match.Candidates);
    }
}
