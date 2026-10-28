using FFXIVSubLedger.Application.Updates;

namespace FFXIVSubLedger.Domain.Tests.Updates;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("V1.10.3", "1.10.3")]
    [InlineData("0.2", "0.2.0")]
    [InlineData("v0.2.0-beta.1", "0.2.0")]
    public void TryParse_ReadsTheNumberOfATag(string tag, string expected)
    {
        Assert.True(AppVersion.TryParse(tag, out Version version));
        Assert.Equal(expected, AppVersion.Describe(version));
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v")]
    public void TryParse_RejectsWhatIsNotAVersion(string tag) =>
        Assert.False(AppVersion.TryParse(tag, out _));

    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("v0.1.1", "0.1.0", true)]
    [InlineData("v0.1.0", "0.1.0", false)]
    [InlineData("v0.0.9", "0.1.0", false)]
    public void IsNewer_ComparesNumbers(string tag, string current, bool expected) =>
        Assert.Equal(expected, AppVersion.IsNewer(tag, new Version(current)));

    [Fact]
    public void IsNewer_TreatsAMissingBuildAsZero() =>
        Assert.True(AppVersion.IsNewer("v0.1", new Version(0, 0, 9)));

    [Fact]
    public void Describe_KeepsThreePartsForReleases()
    {
        Assert.Equal("0.1.0", AppVersion.Describe(new Version(0, 1, 0)));
        Assert.Equal("0.1.0", AppVersion.Describe(new Version(0, 1, 0, 0)));
        Assert.Equal("0.1.0.1", AppVersion.Describe(new Version(0, 1, 0, 1)));
    }
}
