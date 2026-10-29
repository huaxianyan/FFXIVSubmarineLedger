using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

/// <summary>
/// 金额用 long。收艇一年约 3.6 亿，int 上限 21.4 亿——十年下来统计页会直接抛
/// OverflowException（写这个测试时确实抛了）。
/// </summary>
public sealed class MoneyOverflowTests
{
    [Fact]
    public void StatisticsTotalSurvivesAmountsBeyondIntRange()
    {
        var records = new List<LootRecord>();
        for (int i = 0; i < 30; i++)
            records.Add(Record(100_000_000));

        LootStatisticsSnapshot snapshot = LootStatistics.Compute(records);

        Assert.Equal(3_000_000_000L, snapshot.TotalValue);
        Assert.Equal(3_000_000_000L, Assert.Single(snapshot.ItemsByValue).TotalValue);
        Assert.Equal(3_000_000_000L, Assert.Single(snapshot.ByCharacter).TotalValue);
    }

    [Fact]
    public void TrendKeepsAmountsBeyondIntRange()
    {
        var records = new List<LootRecord>();
        for (int i = 0; i < 30; i++)
            records.Add(Record(100_000_000));

        TrendSeries series = LootTrend.Compute(records, TrendGranularity.Day);

        Assert.Equal(3_000_000_000L, Assert.Single(series.Points).TotalValue);
    }

    private static LootRecord Record(long amount) => new()
    {
        CharacterId = Guid.Empty,
        Date = new DateOnly(2026, 1, 1),
        Items =
        [
            new LootItem
            {
                ItemId = 22507,
                ItemName = "沈没船の高級首飾り",
                Quantity = 1,
                UnitPrice = amount,
            },
        ],
    };
}
