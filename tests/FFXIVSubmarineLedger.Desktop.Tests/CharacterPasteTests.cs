using FFXIVSubmarineLedger.Desktop.ViewModels;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 角色页的粘贴：从游戏里复制出来的是「角色名@服务器」，要拆成两个字段。
/// 手打两个字段太麻烦，也别让人自己删那个 @。
/// </summary>
public sealed class CharacterPasteTests
{
    [Theory]
    [InlineData("Tiina Branford@Titan", "Tiina Branford", "Titan")]
    [InlineData("Tiina Branford @ Titan", "Tiina Branford", "Titan")]
    [InlineData("Tiina Branford＠Titan", "Tiina Branford", "Titan")]
    [InlineData("  Tiina Branford @Titan  ", "Tiina Branford", "Titan")]
    [InlineData("Tiina Branford", "Tiina Branford", "")]
    public void SplitsNameAndWorld(string text, string name, string world)
    {
        (string actualName, string actualWorld) = CharacterManagerViewModel.ParseCharacterText(text);

        Assert.Equal(name, actualName);
        Assert.Equal(world, actualWorld);
    }

    [Fact]
    public void TakesTheFirstNonEmptyLine()
    {
        (string name, string world) = CharacterManagerViewModel.ParseCharacterText(
            "\n   \nTiina Branford@Titan\nTiina Branford@Titan");

        Assert.Equal("Tiina Branford", name);
        Assert.Equal("Titan", world);
    }
}
