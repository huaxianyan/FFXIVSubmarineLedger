using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Domain.Tests.Records;

public sealed class LootTrendTests
{
    [Fact]
    public void Compute_FillsDaysWithoutRecordsWithZero()
    {
        var records = new List<LootRecord>
        {
            Record(new DateOnly(2026, 3, 1), 100),
            Record(new DateOnly(2026, 3, 4), 300),
        };

        TrendSeries trend = LootTrend.Compute(records, TrendGranularity.Day);

        Assert.Equal(4, trend.Points.Count);
        Assert.Equal([100, 0, 0, 300], trend.Points.Select(x => x.TotalValue));
        Assert.Equal(300, trend.MaxValue);
    }

    [Fact]
    public void Compute_SumsRecordsInsideTheSameDay()
    {
        var records = new List<LootRecord>
        {
            Record(new DateOnly(2026, 3, 1), 100),
            Record(new DateOnly(2026, 3, 1), 250),
        };

        TrendSeries trend = LootTrend.Compute(records, TrendGranularity.Day);

        Assert.Equal(350, Assert.Single(trend.Points).TotalValue);
    }

    [Fact]
    public void Compute_GroupsByCalendarMonthWhenAsked()
    {
        var records = new List<LootRecord>
        {
            Record(new DateOnly(2026, 1, 20), 100),
            Record(new DateOnly(2026, 2, 3), 200),
            Record(new DateOnly(2026, 2, 27), 50),
        };

        TrendSeries trend = LootTrend.Compute(records, TrendGranularity.Month);

        Assert.Equal(2, trend.Points.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), trend.Points[0].Period);
        Assert.Equal(100, trend.Points[0].TotalValue);
        Assert.Equal(250, trend.Points[1].TotalValue);
    }

    [Fact]
    public void Compute_ReturnsAnEmptySeriesWithoutRecords()
    {
        TrendSeries trend = LootTrend.Compute([], TrendGranularity.Day);

        Assert.Empty(trend.Points);
        Assert.Equal(0, trend.MaxValue);
    }

    private static LootRecord Record(DateOnly date, int value)
    {
        return new LootRecord
        {
            Date = date,
            Items = [new LootItem { ItemName = "ダミー", Quantity = 1, UnitPrice = value }],
        };
    }
}
