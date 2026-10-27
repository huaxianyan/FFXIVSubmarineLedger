using FFXIVSubLedger.Domain.Catalog;
using FFXIVSubLedger.Infrastructure.Catalog;
using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

public sealed class LootScannerTests
{
    private static readonly ItemCatalog Catalog = new(
    [
        new LootCatalogEntry(22507, "沈没船の高級首飾り", 34500, false),
        new LootCatalogEntry(22500, "沈没船の指輪", 8000, false),
        new LootCatalogEntry(23222, "海産油脂", 1, false),
        new LootCatalogEntry(5189, "エメラルド", 2, true),
        new LootCatalogEntry(5069, "ゴールドインゴット", 4, true),
    ]);

    [Fact]
    public void Scan_CollectsLootLinesAndSkipsEverythingElse()
    {
        string[] lines =
        [
            "[11:40] テストさんは p沈没船の高級首飾りX5を入手した。",
            "[11:40] 潜水艦「テスト」の探索完了を確認しました",
            "[11:40] pシャーク級艦尾を修理しました",
            "[11:41] テストさんは pエメラルド29を入手 した。",
        ];

        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(lines, Catalog);

        Assert.Equal(2, loot.Count);
        Assert.Equal([1, 4], loot.Select(x => x.LineNumber));
        Assert.Equal(172500, loot[0].Subtotal);
        Assert.Equal(58, loot[1].Subtotal);
    }

    [Fact]
    public void Scan_CorrectsTheItemNameThatOcrGotOneCharacterWrong()
    {
        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(
            ["[11:40] テストさんは p沈没船の高級括飾りX5を入手した。"],
            Catalog);

        RecognizedLoot entry = Assert.Single(loot);
        Assert.True(entry.IsKnown);
        Assert.Equal("沈没船の高級首飾り", entry.ItemName);
        Assert.Equal(22507u, entry.ItemId);
        Assert.Equal(34500, entry.UnitPrice);
    }

    [Fact]
    public void Scan_FlagsAnItemNameThatFitsMoreThanOneEntry()
    {
        // 「高級括飾り」到「高級首飾り」与「高級耳飾り」都是编辑距离 1，不能默认猜一个。
        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(
            ["[11:40] テストさんは p沈没船の高級括飾りX5を入手した。"],
            new ItemCatalog(
            [
                new LootCatalogEntry(22506, "沈没船の高級耳飾り", 30000, false),
                new LootCatalogEntry(22507, "沈没船の高級首飾り", 34500, false),
            ]));

        RecognizedLoot entry = Assert.Single(loot);
        Assert.True(entry.IsAmbiguous);
        Assert.Equal(2, entry.Candidates.Count);

        // 默认候选取卖店价高的那个。
        Assert.Equal("沈没船の高級首飾り", entry.ItemName);
        Assert.Equal(34500, entry.UnitPrice);
    }

    [Fact]
    public void Scan_AppliesTheHighQualityPriceOnlyWhereTheItemCanBeHighQuality()
    {
        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(
            [
                // ゴールドインゴット 能 HQ：NQ 4 → HQ 5。
                "[11:41] テストさんは pbゴールドインゴットで 25を入手した。",
                // 沈没船の指輪 不能 HQ：多出来的标记只能是符号读歪，价格按 NQ 算。
                "[11:41] テストさんは p沈没船の指輪で 4を入手した。",
            ],
            Catalog);

        Assert.Equal(2, loot.Count);
        Assert.True(loot[0].IsHighQuality);
        Assert.Equal(5, loot[0].UnitPrice);
        Assert.False(loot[1].IsHighQuality);
        Assert.Equal(8000, loot[1].UnitPrice);
    }

    [Fact]
    public void Scan_AssignsLootToTheSubmarineNamedAfterIt()
    {
        // 日志里艇名出现在它那批收益行之后，所以归属是「上一批」。
        string[] lines =
        [
            "[11:40] テストさんは p沈没船の高級首飾りX5を入手した。",
            "[11:40] テストさんは p海産油脂X 3を入手した。",
            "[11:40] 潜水艦「Strawberry」の探索完了を確認しました",
            "[11:40] 潜水艦「Strawberry」が出港しました",
            "[11:41] テストさんは pルビー※ 30を入手した。",
            "[11:41] 潜水艦「Blackberry」の探索完了を確認しました",
        ];

        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(lines, Catalog);

        Assert.Equal(3, loot.Count);
        Assert.All(loot.Take(2), entry => Assert.Equal("Strawberry", entry.SubmarineName));
        Assert.Equal("Blackberry", loot[2].SubmarineName);
    }

    [Fact]
    public void Scan_LeavesLootWithoutAMatchingReturnUnattributed()
    {
        // 收完艇但「探索完了」那行还没刷出来（或者被刷掉了）。
        string[] lines =
        [
            "[11:40] テストさんは p沈没船の指輪X4を入手した。",
            "[11:41] 潜水艦「Strawberry」の探索完了を確認しました",
            "[11:42] テストさんは pルビー×30を入手した。",
        ];

        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(lines, Catalog);

        Assert.Equal(2, loot.Count);
        Assert.Equal("Strawberry", loot[0].SubmarineName);
        Assert.Null(loot[1].SubmarineName);
    }

    [Fact]
    public void Scan_KeepsUnknownItemsWithoutInventingAPrice()
    {
        RecognizedLoot entry = Assert.Single(
            LootScanner.Scan(["[11:41] テストさんは p謎の巻物X3を入手した。"], Catalog));

        Assert.False(entry.IsKnown);
        Assert.Equal("謎の巻物", entry.ItemName);
        Assert.Equal(3, entry.Quantity);
        Assert.Equal(0, entry.UnitPrice);
        Assert.Equal(0, entry.Subtotal);
    }
}
