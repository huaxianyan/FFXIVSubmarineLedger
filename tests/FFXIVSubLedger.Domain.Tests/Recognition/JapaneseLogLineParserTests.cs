using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

public sealed class JapaneseLogLineParserTests
{
    [Theory]
    // 这几条抄的是真实 OCR 输出，符号都被读歪了，看的正是这种输入。
    [InlineData("[11:40] テストさんは p沈没船の高級首飾りX5を入手した。", "沈没船の高級首飾り", 5)]
    [InlineData("[11:40] テストさんは p海産油脂X 3を入手した。", "海産油脂", 3)]
    [InlineData("[11:41] テストさんは pルビー※ 30を入手した。", "ルビー", 30)]
    [InlineData("[11:41] テストさんは pエメラルド29を入手 した。", "エメラルド", 29)]
    [InlineData("[11:41] テストさんは p沈没船の指輪 X4を入手した。", "沈没船の指輪", 4)]
    public void TryParse_ReadsItemAndQuantityThroughOcrNoise(string line, string item, int quantity)
    {
        Assert.True(JapaneseLogLineParser.TryParse(line, out LootLine parsed));
        Assert.Equal(item, parsed.ItemText);
        Assert.Equal(quantity, parsed.Quantity);
        Assert.Equal(line, parsed.RawLine);
    }

    [Fact]
    public void TryParse_SeesTheHighQualityMarkerBeforeTheQuantity()
    {
        // OCR 把 ◉ 读成了「で」。
        Assert.True(JapaneseLogLineParser.TryParse(
            "[11:41] テストさんは pbゴールドインゴットで 25を入手した。",
            out LootLine parsed));

        Assert.Equal("ゴールドインゴット", parsed.ItemText);
        Assert.Equal(25, parsed.Quantity);
        Assert.True(parsed.LooksHighQuality);
    }

    [Theory]
    [InlineData("[11:40] 潜水艦「テスト」の探索完了を確認しました")]
    [InlineData("[11:40] pシャーク級艦尾を修理しました")]
    [InlineData("[11:40] 潜水艦「テスト」が出港しました")]
    [InlineData("[11:40] テストさんは p沈没船の指輪を入手した。")]
    [InlineData("")]
    public void TryParse_IgnoresLinesThatAreNotLoot(string line)
    {
        Assert.False(JapaneseLogLineParser.TryParse(line, out _));
    }
}
