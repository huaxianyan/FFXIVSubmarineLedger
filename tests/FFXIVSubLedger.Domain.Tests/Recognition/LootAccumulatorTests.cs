using FFXIVSubLedger.Domain.Catalog;
using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

public sealed class LootAccumulatorTests
{
    private const string NecklaceLine = "[10:45] テストさんは沈没船の高級首飾り×5を入手した。";

    [Fact]
    public void TakeNew_ReturnsEverythingOnceThenNothing()
    {
        var accumulator = new LootAccumulator();
        IReadOnlyList<RecognizedLoot> entries = [Entry("Strawberry", NecklaceLine)];

        Assert.Single(accumulator.TakeNew(entries));
        Assert.Empty(accumulator.TakeNew(entries));
    }

    [Fact]
    public void TakeNew_KeepsIdenticalLinesThatBelongToDifferentSubmarines()
    {
        // 这条是真实踩过的坑：同一句「高級耳飾り×5を入手した。」会分属两艘艇，
        // 按文本去重会把后一艘艇的那条吞掉，钱就串到前一艘艇名下。
        var accumulator = new LootAccumulator();
        IReadOnlyList<RecognizedLoot> entries =
        [
            Entry("Blackberry", NecklaceLine),
            Entry("Cranberry", NecklaceLine),
        ];

        IReadOnlyList<RecognizedLoot> fresh = accumulator.TakeNew(entries);

        Assert.Equal(2, fresh.Count);
        Assert.Equal(["Blackberry", "Cranberry"], fresh.Select(x => x.SubmarineName));
        Assert.Empty(accumulator.TakeNew(entries));
    }

    [Fact]
    public void TakeNew_CountsRepeatsInsideTheSameSubmarine()
    {
        var accumulator = new LootAccumulator();
        IReadOnlyList<RecognizedLoot> one = [Entry("Blackberry", NecklaceLine)];
        IReadOnlyList<RecognizedLoot> two = [Entry("Blackberry", NecklaceLine), Entry("Blackberry", NecklaceLine)];

        Assert.Single(accumulator.TakeNew(one));
        Assert.Single(accumulator.TakeNew(two));
        Assert.Empty(accumulator.TakeNew(two));
    }

    [Fact]
    public void TakeNew_HandlesTheRingBufferDroppingTheOldestLines()
    {
        var accumulator = new LootAccumulator();
        accumulator.TakeNew([Entry("Strawberry", NecklaceLine), Entry("Blackberry", NecklaceLine)]);

        // 环形表写满后最早的行被顶掉了，整段内容往前移。
        IReadOnlyList<RecognizedLoot> shifted =
        [
            Entry("Blackberry", NecklaceLine),
            Entry("Blueberry", NecklaceLine),
        ];

        IReadOnlyList<RecognizedLoot> fresh = accumulator.TakeNew(shifted);

        Assert.Equal("Blueberry", Assert.Single(fresh).SubmarineName);
    }

    [Fact]
    public void TakeNew_DeliversALineOnceItsSubmarineIsKnown()
    {
        var accumulator = new LootAccumulator();

        Assert.Empty(accumulator.TakeNew([]));

        IReadOnlyList<RecognizedLoot> withSub = [Entry("Cranberry", NecklaceLine)];
        Assert.Single(accumulator.TakeNew(withSub));
    }

    private static RecognizedLoot Entry(string submarine, string rawLine) => new(
        LineNumber: 1,
        ItemText: "沈没船の高級首飾り",
        Item: new LootCatalogEntry(22507, "沈没船の高級首飾り", 34500, false),
        Candidates: [],
        ItemName: "沈没船の高級首飾り",
        ItemId: 22507,
        Quantity: 5,
        IsHighQuality: false,
        UnitPrice: 34500,
        SubmarineName: submarine,
        RawLine: rawLine);
}
