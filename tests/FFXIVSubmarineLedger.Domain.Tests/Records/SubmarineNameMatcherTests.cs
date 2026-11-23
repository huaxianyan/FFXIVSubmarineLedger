using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

public sealed class SubmarineNameMatcherTests
{
    // 用户的真实编制：2026-11-22 的一次截图把前三艘读歪了，结果凭空多出三艘各一条记录的艇。
    private static readonly string[] Fleet = ["Blackberry", "Blueberry", "Cranberry", "Strawberry"];

    [Theory]
    [InlineData("Strawberrv", "Strawberry")]
    [InlineData("Blackberrvy", "Blackberry")]
    [InlineData("Blug5erry", "Blueberry")]
    public void Guess_MapsReadErrorsBackOntoTheExistingSubmarine(string read, string expected)
    {
        SubmarineNameGuess guess = SubmarineNameMatcher.Guess(read, Fleet);

        Assert.Equal(SubmarineNameKind.Corrected, guess.Kind);
        Assert.Equal(expected, guess.Name);
        Assert.Empty(guess.NearNames);
    }

    [Fact]
    public void Guess_LeavesExactNamesAlone()
    {
        SubmarineNameGuess guess = SubmarineNameMatcher.Guess("Cranberry", Fleet);

        Assert.Equal(SubmarineNameKind.Exact, guess.Kind);
        Assert.Equal("Cranberry", guess.Name);
    }

    [Fact]
    public void Guess_TreatsADifferentNameAsANewSubmarine()
    {
        SubmarineNameGuess guess = SubmarineNameMatcher.Guess("Watermelon", Fleet);

        Assert.Equal(SubmarineNameKind.New, guess.Kind);
        Assert.Null(guess.Name);
    }

    [Fact]
    public void Guess_DoesNotPickWhenTwoSubmarinesAreEquallyClose()
    {
        // 两艘一样近的时候挑哪个都会错，交回人工，不静默取第一个。
        SubmarineNameGuess guess = SubmarineNameMatcher.Guess("Sub9", ["Sub8", "Sub0"]);

        Assert.Equal(SubmarineNameKind.Ambiguous, guess.Kind);
        Assert.Null(guess.Name);
        Assert.Equal(["Sub0", "Sub8"], guess.NearNames);
    }

    [Fact]
    public void Guess_NeverBendsNumberedNamesOntoEachOther()
    {
        // 「Sub1」「Sub2」这种编号命名很常见，差一个字就是另一艘艇：只给候选，不自动改写。
        SubmarineNameGuess guess = SubmarineNameMatcher.Guess("Sub2", ["Sub1"]);

        Assert.Equal(SubmarineNameKind.Ambiguous, guess.Kind);
        Assert.Null(guess.Name);
        Assert.Equal(["Sub1"], guess.NearNames);
    }

    [Fact]
    public void Guess_ReportsUnmarkedWhenTheLogCarriedNoName()
    {
        Assert.Equal(SubmarineNameKind.Unmarked, SubmarineNameMatcher.Guess(null, Fleet).Kind);
        Assert.Equal(SubmarineNameKind.Unmarked, SubmarineNameMatcher.Guess(string.Empty, Fleet).Kind);
    }

    [Fact]
    public void Guess_ToleratesLessOnShortNames()
    {
        // 4 个字的艇名差一个字可能就是另一艘（编号命名），差两个连候选都不给。
        Assert.Equal(SubmarineNameKind.Ambiguous, SubmarineNameMatcher.Guess("Alfa", ["Alfx"]).Kind);
        Assert.Equal(SubmarineNameKind.New, SubmarineNameMatcher.Guess("Alfa", ["Alzy"]).Kind);
    }
}
