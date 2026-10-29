using Avalonia.Headless.XUnit;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using FFXIVSubmarineLedger.Infrastructure.Parsing;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 落库用的道具名必须与界面语言无关。曾经这里取的是显示名，结果中文界面下记账会把
/// 「上等沉船戒指」写进库，日文界面下写「沈没船の高級指輪」——同一种道具在库里有两个名字。
/// </summary>
[Collection(LocalizationCollection.Name)]
public sealed class LootEntryNamingTests
{
    private static readonly LootCatalogEntry Platinum = new(
        9360,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ja"] = "プラチナインゴット",
            ["zh-Hans"] = "白金锭",
        },
        4,
        true);

    [AvaloniaFact]
    public void EntryStoresTheCanonicalNameWhateverTheUiLanguage()
    {
        try
        {
            Loc.Instance.SetLanguage("zh-Hans");
            LootRowViewModel chinese = Row();
            Assert.Equal("白金锭", chinese.ItemName);
            Assert.Equal("プラチナインゴット", chinese.ToEntry().ItemName);
            Assert.Equal(9360u, chinese.ToEntry().ItemId);

            Loc.Instance.SetLanguage("ja");
            LootRowViewModel japanese = Row();
            Assert.Equal("プラチナインゴット", japanese.ItemName);
            Assert.Equal("プラチナインゴット", japanese.ToEntry().ItemName);
        }
        finally
        {
            Loc.Instance.SetLanguage("zh-Hans");
        }
    }

    [AvaloniaFact]
    public void ItemOutsideTheCatalogKeepsTheRecognizedText()
    {
        var unknown = new RecognizedLoot(
            LineNumber: 1,
            ItemText: "プラチナインゴット",
            Item: null,
            Candidates: [],
            ItemName: "プラチナインゴット",
            ItemId: 0,
            Quantity: 20,
            IsHighQuality: false,
            UnitPrice: 4,
            SubmarineName: "Blackberry",
            RawLine: "テスト");

        var row = new LootRowViewModel(unknown, [new SubmarineChoice(string.Empty)]);
        row.UnitPriceOverride = 4;

        Assert.Equal("プラチナインゴット", row.ToEntry().ItemName);
        Assert.Equal(0u, row.ToEntry().ItemId);
        Assert.True(row.ToEntry().IsUnknown);
    }

    private static LootRowViewModel Row()
    {
        var loot = new RecognizedLoot(
            LineNumber: 1,
            ItemText: "プラチナインゴット",
            Item: Platinum,
            Candidates: [],
            ItemName: "プラチナインゴット",
            ItemId: 9360,
            Quantity: 20,
            IsHighQuality: false,
            UnitPrice: 4,
            SubmarineName: "Blackberry",
            RawLine: "テスト");

        return new LootRowViewModel(loot, [new SubmarineChoice(string.Empty)]);
    }
}
