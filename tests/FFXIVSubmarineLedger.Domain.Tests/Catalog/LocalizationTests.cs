using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Domain.Settings;
using FFXIVSubmarineLedger.Infrastructure.Catalog;

namespace FFXIVSubmarineLedger.Domain.Tests.Catalog;

/// <summary>
/// 语言资源与多语言物名。加语言时的护栏：少键、少标签都会在这里报出来。
/// </summary>
public sealed class LocalizationTests
{
    [Fact]
    public void EveryLanguageHasTheSameKeys()
    {
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> keySets = Loc.KeySets;
        IReadOnlyCollection<string> reference = keySets[ItemDisplayLanguage.Fallback];
        var problems = new List<string>();

        foreach ((string language, IReadOnlyCollection<string> keys) in keySets)
        {
            foreach (string missing in reference.Except(keys))
                problems.Add($"{language} 缺 {missing}");

            foreach (string extra in keys.Except(reference))
                problems.Add($"{language} 多出 {extra}");
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void EveryLanguageNamesItself()
    {
        Assert.NotEmpty(Loc.Available);

        foreach (LanguageInfo language in Loc.Available)
        {
            Assert.False(string.IsNullOrWhiteSpace(language.Tag));
            Assert.False(string.IsNullOrWhiteSpace(language.Name));
        }
    }

    [Fact]
    public void SwitchingLanguageChangesVisibleText()
    {
        string chinese = Loc.Instance[Loc.Available[0].Tag == "ja" ? "ja" : ItemDisplayLanguage.Fallback];

        try
        {
            Assert.True(Loc.Instance.SetLanguage("ja"));
            Assert.Equal("設定", Loc.Get("nav.settings"));

            Assert.True(Loc.Instance.SetLanguage(ItemDisplayLanguage.Fallback));
            Assert.Equal("设置", Loc.Get("nav.settings"));
        }
        finally
        {
            Loc.Instance.SetLanguage(ItemDisplayLanguage.Fallback);
        }

        Assert.False(string.IsNullOrEmpty(chinese));
    }

    [Fact]
    public void UnknownLanguageIsRefused()
    {
        Assert.False(Loc.Instance.SetLanguage("tlh"));
        Assert.Equal(ItemDisplayLanguage.Fallback, Loc.Instance.Language);
    }

    [Fact]
    public void ItemNameFollowsTheDisplayLanguage()
    {
        var entry = new LootCatalogEntry(
            22507,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ja"] = "沈没船の高級首飾り",
                ["zh-Hans"] = "上等沉船项链",
            },
            34500,
            false);

        try
        {
            ItemDisplayLanguage.Current = "zh-Hans";
            Assert.Equal("上等沉船项链", entry.DisplayName);

            ItemDisplayLanguage.Current = "ja";
            Assert.Equal("沈没船の高級首飾り", entry.DisplayName);

            // 没有这门语言时退回表里第一门，而不是显示空白。
            ItemDisplayLanguage.Current = "fr";
            Assert.Equal("沈没船の高級首飾り", entry.DisplayName);
        }
        finally
        {
            ItemDisplayLanguage.Current = ItemDisplayLanguage.Fallback;
        }
    }

    [Fact]
    public void TheBundledCatalogCarriesEveryLanguage()
    {
        ItemCatalog catalog = ItemCatalog.Load();
        Assert.NotEmpty(catalog.Entries);

        foreach (LootCatalogEntry entry in catalog.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.NameIn("ja")), $"ItemId {entry.ItemId} 缺日文名");
            Assert.False(string.IsNullOrWhiteSpace(entry.NameIn("zh-Hans")), $"ItemId {entry.ItemId} 缺中文名");
            Assert.True(entry.ShopPriceNq > 0, $"ItemId {entry.ItemId} 价格不对");
        }
    }
}
