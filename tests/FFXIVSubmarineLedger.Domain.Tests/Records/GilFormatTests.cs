using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

/// <summary>
/// 大额金额的两种写法：主显示是带单位的概数（只看量级，不带小数），
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
    [InlineData(19_999, "1 万 Gil")]
    [InlineData(123_456, "12 万 Gil")]
    [InlineData(995_392, "99 万 Gil")]
    [InlineData(99_999_999, "9999 万 Gil")]
    [InlineData(100_000_000, "1 亿 Gil")]
    [InlineData(363_215_608, "3 亿 Gil")]
    public void Compact_ShowsOnlyTheMagnitude(long value, string expected) =>
        Assert.Equal(expected, GilFormat.Compact(value));

    [Fact]
    public void Compact_HasNoDecimalPointAtAll()
    {
        foreach (long value in new[] { 10_000L, 15_500, 123_456, 99_999_999, 150_000_000, 363_215_608 })
            Assert.DoesNotContain(".", GilFormat.Compact(value), StringComparison.Ordinal);
    }

    [Fact]
    public void Compact_StaysOnTenThousandUntilItActuallyReachesHundredMillion()
    {
        // 没满一亿就还是「万」，满了才换单位。
        Assert.StartsWith("9999 万", GilFormat.Compact(99_999_999), StringComparison.Ordinal);
        Assert.StartsWith("1 亿", GilFormat.Compact(100_000_000), StringComparison.Ordinal);
        Assert.StartsWith("1 亿", GilFormat.Compact(199_999_999), StringComparison.Ordinal);
    }

    [Fact]
    public void CompactNumber_LeavesOutTheUnitForChartAxes()
    {
        Assert.Equal("9,999", GilFormat.CompactNumber(9_999));
        Assert.Equal("99 万", GilFormat.CompactNumber(995_392));
        Assert.Equal("3 亿", GilFormat.CompactNumber(363_215_608));
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
            Assert.Equal("3 亿 Gil", GilFormat.Compact(363_215_608));

            Loc.Instance.SetLanguage("ja");
            Assert.Equal("3 億 Gil", GilFormat.Compact(363_215_608));
        }
        finally
        {
            Loc.Instance.SetLanguage("zh-Hans");
        }
    }
}
