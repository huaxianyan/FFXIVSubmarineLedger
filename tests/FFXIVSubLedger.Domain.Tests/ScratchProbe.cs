using FFXIVSubLedger.Domain.Catalog;
using FFXIVSubLedger.Infrastructure.Catalog;
using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Domain.Tests;

public sealed class ScratchProbe
{
    [Fact]
    public void ProbeExact()
    {
        var catalog = new ItemCatalog(
        [
            new LootCatalogEntry(22507, "沈没船の高級首飾り", 34500, false),
            new LootCatalogEntry(22500, "沈没船の指輪", 8000, false),
            new LootCatalogEntry(23222, "海産油脂", 1, false),
            new LootCatalogEntry(5189, "エメラルド", 2, true),
            new LootCatalogEntry(5069, "ゴールドインゴット", 4, true),
        ]);

        string[] lines =
        [
            "[10:45] テストさんはルビー×28を入手した。",
            "[10:45] テストさんはルビー[HQ]×29を入手した。",
            "[10:45] 潜水艦「Strawberry」の探索完了を確認しました。",
        ];

        bool a = JapaneseLogLineParser.TryParse(lines[1], out LootLine p1);
        bool b = JapaneseLogLineParser.TryParseSubmarineReturn(lines[2], out string sub);

        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(lines, catalog);

        Assert.True(loot.Count == 2, $"count={loot.Count} 第二行解析={a} 物名={p1?.ItemText} HQ={p1?.LooksHighQuality} 探索行={b} 艇={sub}");
    }
}
