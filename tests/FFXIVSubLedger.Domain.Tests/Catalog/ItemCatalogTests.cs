using FFXIVSubLedger.Domain.Catalog;
using FFXIVSubLedger.Infrastructure.Catalog;
using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Domain.Tests.Catalog;

public sealed class ItemCatalogTests
{
    [Fact]
    public void Load_ReadsTheShippedJapaneseTable()
    {
        ItemCatalog catalog = ItemCatalog.Load();

        // 12 条基础潜水艦掉落 + 12 条宝石类（含スタールビー、スターサファイア）。
        Assert.Equal(24, catalog.Entries.Count);

        // 聊天框直读会带来截图里没出现过的宝石，词表少一条就会变成「未命中」。
        LootCatalogEntry starRuby = Assert.IsType<LootCatalogEntry>(catalog.Find(12544));
        Assert.Equal("スタールビー", starRuby.Name);
        Assert.Equal(3, starRuby.ShopPriceNq);

        LootCatalogEntry necklace = Assert.IsType<LootCatalogEntry>(catalog.Find(22507));
        Assert.Equal("沈没船の高級首飾り", necklace.Name);
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
              { "itemId": 1, "name": "あ", "shopPriceNq": 1, "canBeHq": false },
              { "itemId": 1, "name": "い", "shopPriceNq": 2, "canBeHq": false }
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
            new(1, "沈没船の首飾り", 13000, false),
            new(2, "沈没船の高級首飾り", 34500, false),
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
            new(1, "テストアイテム甲", 10, false),
            new(2, "テストアイテム乙", 20, false),
        };

        ItemMatch match = ItemMatcher.Match("テストアイテム丙", entries);

        Assert.True(match.IsAmbiguous);
        Assert.Equal(2, match.Candidates.Count);
    }

    [Fact]
    public void Match_GivesUpWhenNothingIsCloseEnough()
    {
        ItemMatch match = ItemMatcher.Match("まったく別の物", [new LootCatalogEntry(1, "エメラルド", 2, true)]);

        Assert.Null(match.Item);
        Assert.Empty(match.Candidates);
    }
}
