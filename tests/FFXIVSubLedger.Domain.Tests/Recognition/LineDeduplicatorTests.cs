using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

public sealed class LineDeduplicatorTests
{
    [Fact]
    public void TakeNew_ReturnsEverythingTheFirstTime()
    {
        var deduplicator = new LineDeduplicator();

        IReadOnlyList<string> fresh = deduplicator.TakeNew(["あ", "い"]);

        Assert.Equal(["あ", "い"], fresh);
    }

    [Fact]
    public void TakeNew_SkipsLinesThatWereAlreadyHandled()
    {
        var deduplicator = new LineDeduplicator();
        deduplicator.TakeNew(["あ", "い"]);

        Assert.Empty(deduplicator.TakeNew(["あ", "い"]));
    }

    [Fact]
    public void TakeNew_SkipsTheWholeBatchWhenNothingNewArrived()
    {
        // 实测确认：读取器每次调用都把整段累计历史再给一遍。
        var deduplicator = new LineDeduplicator();
        deduplicator.TakeNew(["あ", "い"]);

        Assert.Empty(deduplicator.TakeNew(["あ", "い"]));
    }

    [Fact]
    public void TakeNew_CountsRepeatedLinesByOccurrence()
    {
        // 同一种道具分两行入包时日志里是两条一模一样的行。
        // 第一条先到，第二条后到，两条都必须算新；第三次再回放就不能重复计。
        var deduplicator = new LineDeduplicator();

        Assert.Single(deduplicator.TakeNew(["エメラルド×25を入手した。"]));
        Assert.Single(deduplicator.TakeNew(["エメラルド×25を入手した。", "エメラルド×25を入手した。"]));
        Assert.Empty(deduplicator.TakeNew(["エメラルド×25を入手した。", "エメラルド×25を入手した。"]));
    }

    [Fact]
    public void TakeNew_HandlesABatchThatGrowsBetweenReads()
    {
        var deduplicator = new LineDeduplicator();
        deduplicator.TakeNew(["あ"]);

        IReadOnlyList<string> fresh = deduplicator.TakeNew(["あ", "い"]);

        Assert.Equal(["い"], fresh);
    }

    [Fact]
    public void TakeNew_ReturnsOnlyTheNewTailWhenTheBufferReplaysEverything()
    {
        var deduplicator = new LineDeduplicator();
        deduplicator.TakeNew(["あ", "い"]);

        Assert.Empty(deduplicator.TakeNew(["あ", "い"]));
        Assert.Equal(["う"], deduplicator.TakeNew(["あ", "い", "う"]));
    }

    [Fact]
    public void TakeNew_KeepsWorkingAfterTheRingBufferWraps()
    {
        // 环形表写满后会顶掉最早的行：整批内容变了，但已在跟踪的行不应重新出现。
        var deduplicator = new LineDeduplicator();
        deduplicator.TakeNew(["あ", "い", "う"]);

        Assert.Empty(deduplicator.TakeNew(["い", "う"]));
        Assert.Equal(["え"], deduplicator.TakeNew(["い", "う", "え"]));
    }
}
