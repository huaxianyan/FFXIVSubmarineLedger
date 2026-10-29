using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

/// <summary>大额金额的两种写法：完整千分位给账本，紧凑写法给量级。</summary>
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
    [InlineData(9_999, null)]
    [InlineData(10_000, "1 万 Gil")]
    [InlineData(123_456, "12.3 万 Gil")]
    [InlineData(99_995_000, "9999.5 万 Gil")]
    [InlineData(99_999_999, "9999.9 万 Gil")]
    public void Compact_StaysOnTenThousandUntilItActuallyReachesHundredMillion(long value, string? expected)
    {
        string compact = GilFormat.Compact(value);

        if (expected is null)
        {
            Assert.Equal(GilFormat.Full(value), compact);
            return;
        }

        Assert.Equal(expected, compact);
    }

    [Fact]
    public void Compact_SwitchesToHundredMillionOnlyWhenFull()
    {
        // 没满一亿就还是「万」，满了才换单位；四舍五入不能把 9999.99 万 变成 10000 万。
        Assert.StartsWith("9999.9 万", GilFormat.Compact(99_999_999));
        Assert.StartsWith("1 亿", GilFormat.Compact(100_000_000));
        Assert.StartsWith("1.99 亿", GilFormat.Compact(199_999_999));
    }

    [Fact]
    public void CompactNumber_LeavesOutTheUnitForChartAxes()
    {
        Assert.Equal("9,999", GilFormat.CompactNumber(9_999));
        Assert.Equal("99.5 万", GilFormat.CompactNumber(995_392));
        Assert.Equal("995.3 万", GilFormat.CompactNumber(9_953_900));
        Assert.Equal("3.63 亿", GilFormat.CompactNumber(363_215_608));
    }

    [Fact]
    public void Compact_UsesHundredMillionForLargeTotals()
    {
        try
        {
            Loc.Instance.SetLanguage("zh-Hans");
            Assert.Equal("3.63 亿 Gil", GilFormat.Compact(363_215_608));

            Loc.Instance.SetLanguage("ja");
            Assert.Equal("3.63 億 Gil", GilFormat.Compact(363_215_608));
        }
        finally
        {
            Loc.Instance.SetLanguage("zh-Hans");
        }
    }
}
