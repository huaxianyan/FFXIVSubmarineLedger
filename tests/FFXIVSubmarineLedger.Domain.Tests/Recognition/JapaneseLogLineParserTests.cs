using FFXIVSubmarineLedger.Infrastructure.Parsing;

namespace FFXIVSubmarineLedger.Domain.Tests.Recognition;

public sealed class JapaneseLogLineParserTests
{
    [Theory]
    // 这两条来自真实聊天框的内存读取结果（角色名已替换）。它们没有截图里的 ▶ 标记，
    // 「×」也更接近半角，解析器要两种都吃得下。
    [InlineData("[16:11] テストさんは転送網利用券:蒼天街×10を入手した。", "転送網利用券:蒼天街", 10)]
    // 聊天框直读时 HQ 写成物名后缀 [HQ]（截图渲染则是 ◉），不能让它混进物名。
    [InlineData("[10:45] テストさんはルビー[HQ]×29を入手した。", "ルビー", 29)]
    [InlineData("[11:40] テストさんは p沈没船の高級首飾りX5を入手した。", "沈没船の高級首飾り", 5)]
    [InlineData("[11:40] テストさんは p海産油脂X 3を入手した。", "海産油脂", 3)]
    [InlineData("[11:41] テストさんは pルビー※ 30を入手した。", "ルビー", 30)]
    [InlineData("[11:41] テストさんは pエメラルド29を入手 した。", "エメラルド", 29)]
    [InlineData("[11:41] テストさんは p沈没船の指輪 X4を入手した。", "沈没船の指輪", 4)]
    // 2026-11-22 的真实截图：▶ 被读成「5」「5p」，× 被读成「メ」「ぶ」。
    [InlineData("[11:41] テストさんは 5ルビーメ27を入手した。", "ルビー", 27)]
    [InlineData("[11:41] テストさんは 5pエメラルドで 30を入手した。", "エメラルド", 30)]
    [InlineData("[11:41] テストさんは pルビー※ぶ27を入手した。", "ルビー", 27)]
    [InlineData("[11:41] テストさんは 5スターサファイアメメ27を入手した。", "スターサファイア", 27)]
    public void TryParse_ReadsItemAndQuantityThroughOcrNoise(string line, string item, int quantity)
    {
        Assert.True(JapaneseLogLineParser.TryParse(line, out LootLine parsed));
        Assert.Equal(item, parsed.ItemText);
        Assert.Equal(quantity, parsed.Quantity);
        Assert.Equal(line, parsed.RawLine);
    }

    [Theory]
    [InlineData("[11:41] Tiina Branfordは 沈没船の指輪×4を入手した。", "TiinaBranford")]
    [InlineData("[11:41] Tiina Branford はルビー×4を入手した。", "TiinaBranford")]
    [InlineData("[11:41] テストさんはルビー×4を入手した。", "テスト")]
    public void TryParse_TakesTheCharacterNameAsTheOwner(string line, string owner)
    {
        Assert.True(JapaneseLogLineParser.TryParse(line, out LootLine parsed));
        Assert.Equal(owner, parsed.ActorName);
    }

    [Fact]
    public void TryParse_RejectsALineWithoutAnActor()
    {
        // 「〜の景品として」这类没有主语的行不是收艇收益，也不能靠猜角色归属。
        Assert.False(JapaneseLogLineParser.TryParse(
            "[11:41] 沈没船の指輪×4を入手した。",
            out _));
    }

    [Fact]
    public void TryParse_SeesTheHighQualitySuffixUsedByTheChatLog()
    {
        Assert.True(JapaneseLogLineParser.TryParse(
            "[10:45] テストさんはルビー[HQ]×29を入手した。",
            out LootLine parsed));

        Assert.Equal("ルビー", parsed.ItemText);
        Assert.Equal(29, parsed.Quantity);
        Assert.True(parsed.LooksHighQuality);
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
    [InlineData("[11:40] 潜水艦「Strawberry」の探索完了を確認しました", "Strawberry")]
    // 日志窗口底部压在亮色场景上的那几行带半透明，OCR 常把第一个字读错，艇名照样得读出来。
    [InlineData("[11:41] 替水艦「Cranberry」の探索完了を確認しました", "Cranberry")]
    [InlineData("[11:41] 深水艦「Cranberry」の探索完了を確認しました", "Cranberry")]
    // 解析前会去掉空白，所以带空格的艇名读出来是不带空格的；归属判定用同一套规则，不受影响。
    [InlineData("[11:41] 潜水艦「Black berry」の探索完了を確認しました。", "Blackberry")]
    public void TryParseSubmarineReturn_ReadsTheSubmarineName(string line, string expected)
    {
        Assert.True(JapaneseLogLineParser.TryParseSubmarineReturn(line, out string name));
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("[11:40] 潜水艦「テスト」が出港しました")]
    [InlineData("[11:40] テストさんは p沈没船の指輪X4を入手した。")]
    [InlineData("")]
    public void TryParseSubmarineReturn_IgnoresOtherLines(string line)
    {
        Assert.False(JapaneseLogLineParser.TryParseSubmarineReturn(line, out _));
    }

    [Theory]
    [InlineData("[11:40] 潜水艦「テスト」の探索完了を確認しました")]
    [InlineData("[11:40] pシャーク級艦尾を修理しました")]
    [InlineData("[11:40] 潜水艦「テスト」が出港しました")]
    [InlineData("[11:40] テストさんは p沈没船の指輪を入手した。")]
    // 真实聊天记录里这种没有主语的景品行不能被当成收益。
    [InlineData("[16:09] クポフォーチュンの景品として、マジックプリズム:フォルタン×10を入手しました。")]
    [InlineData("")]
    public void TryParse_IgnoresLinesThatAreNotLoot(string line)
    {
        Assert.False(JapaneseLogLineParser.TryParse(line, out _));
    }
}
