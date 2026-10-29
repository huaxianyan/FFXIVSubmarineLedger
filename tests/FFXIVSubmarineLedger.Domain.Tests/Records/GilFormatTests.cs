using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

/// <summary>
/// 大额金额的两种写法：主显示是带单位的概数（看量级，最多一位小数），
/// 副行是精确值（核对用）。
/// </summary>
[Collection(LocalizationCollection.Name)]
public sealed class GilFormatTests
{
    [Theory]
    [InlineData(0, "0 Gil")]
    [InlineData(995_392, "995,392 Gil")]
    [InlineData(363_215_608, "363,215,608 Gil")]
    public void Full_KeepsEveryDigit(long value, string expected) =>
        Assert.Equal(expected, GilFormat.Full(value));

    [Theory]
    [InlineData(9_999, "9,999 Gil")]
    [InlineData(10_000, "1 万 Gil")]
    [InlineData(19_999, "1.9 万 Gil")]
    [InlineData(123_456, "12.3 万 Gil")]
    [InlineData(995_392, "99.5 万 Gil")]
    [InlineData(99_999_999, "9999.9 万 Gil")]
    [InlineData(100_000_000, "1 亿 Gil")]
    [InlineData(363_215_608, "3.6 亿 Gil")]
    public void Compact_KeepsAtMostOneDecimal(long value, string expected) =>
        Assert.Equal(expected, GilFormat.Compact(value));

    [Fact]
    public void Compact_NeverShowsMoreThanOneDecimal()
    {
        foreach (long value in new[] { 10_000L, 15_500, 123_456, 99_999_999, 150_000_000, 363_215_608 })
        {
            string compact = GilFormat.Compact(value);
            int dot = compact.IndexOf('.', StringComparison.Ordinal);

            if (dot < 0)
                continue;

            // 小数点后到空格之间就是小数位
            int space = compact.IndexOf(' ', dot);
            int digits = (space < 0 ? compact.Length : space) - dot - 1;

            Assert.True(digits <= 1, $"{compact} 有 {digits} 位小数");
        }
    }

    [Fact]
    public void Compact_TruncatesInsteadOfRounding()
    {
        // 四舍五入会把 9999.99 万 变成「10000 万」——既不是一万万也不是一亿，量级是错的。
        Assert.StartsWith("9999.9 万", GilFormat.Compact(99_999_999), StringComparison.Ordinal);
        Assert.StartsWith("1 亿", GilFormat.Compact(100_000_000), StringComparison.Ordinal);
        Assert.StartsWith("1.9 亿", GilFormat.Compact(199_999_999), StringComparison.Ordinal);
    }

    [Fact]
    public void CompactNumber_LeavesOutTheUnitForChartAxes()
    {
        Assert.Equal("9,999", GilFormat.CompactNumber(9_999));
        Assert.Equal("99.5 万", GilFormat.CompactNumber(995_392));
        Assert.Equal("3.6 亿", GilFormat.CompactNumber(363_215_608));
    }

    [Fact]
    public void HasDetail_OnlyWhenTheCompactFormLosesInformation()
    {
        Assert.False(GilFormat.HasDetail(9_999));
        Assert.True(GilFormat.HasDetail(10_000));
    }

    [Fact]
    public void Compact_UsesTheCurrentLanguageUnit()
    {
        try
        {
            Loc.Instance.SetLanguage("zh-Hans");
            Assert.Equal("3.6 亿 Gil", GilFormat.Compact(363_215_608));

            Loc.Instance.SetLanguage("ja");
            Assert.Equal("3.6 億 Gil", GilFormat.Compact(363_215_608));
        }
        finally
        {
            Loc.Instance.SetLanguage("zh-Hans");
        }
    }
}
