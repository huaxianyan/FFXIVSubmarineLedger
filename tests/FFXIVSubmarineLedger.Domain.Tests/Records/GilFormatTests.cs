using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

/// <summary>大额金额的两种写法：完整千分位给账本，紧凑写法给量级。</summary>
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
    [InlineData(99_990_000, "9999 万 Gil")]
    public void Compact_UsesTenThousandUpToAHundredMillion(long value, string? expected)
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
