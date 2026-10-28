using System.Text.RegularExpressions;
using FFXIVSubLedger.Application.Localization;
using Xunit;

namespace FFXIVSubLedger.Desktop.Tests;

/// <summary>
/// 界面里用到的每个文案键都必须在每门语言里存在。
/// 漏键时界面会直接显示键名（比如 <c>stats.total</c>），比错译更容易被忽略。
/// </summary>
public sealed partial class LocalizationKeyTests
{
    [Fact]
    public void EveryKeyUsedInViewsExistsInEveryLanguage()
    {
        string views = Path.Combine(AppContext.BaseDirectory, "Views");
        Assert.True(Directory.Exists(views), $"测试缺少视图副本：{views}");

        var missing = new List<string>();

        foreach (string file in Directory.EnumerateFiles(views, "*.axaml"))
        {
            string xaml = File.ReadAllText(file);

            foreach (Match match in TrKey().Matches(xaml))
            {
                string key = match.Groups[1].Value;

                foreach ((string language, IReadOnlyCollection<string> keys) in Loc.KeySets)
                {
                    if (!keys.Contains(key))
                        missing.Add($"{Path.GetFileName(file)}: {key} 在 {language} 里没有");
                }
            }
        }

        Assert.Empty(missing);
    }

    [GeneratedRegex(@"\{loc:Tr\s+([A-Za-z0-9_.]+)\s*\}")]
    private static partial Regex TrKey();
}
