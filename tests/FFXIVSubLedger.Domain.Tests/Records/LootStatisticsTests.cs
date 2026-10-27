using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Domain.Tests.Records;

public sealed class LootStatisticsTests
{
    [Fact]
    public void Compute_ReturnsZerosWhenThereAreNoRecords()
    {
        LootStatisticsSnapshot stats = LootStatistics.Compute([]);

        Assert.Equal(0, stats.TotalValue);
        Assert.Equal(0, stats.OperatingDays);
        Assert.Null(stats.FirstDate);
        Assert.Empty(stats.ItemsByValue);
    }

    [Fact]
    public void Compute_DividesByTheCalendarSpanNotByRecordCount()
    {
        // 1 月 1 日与 1 月 10 日各一笔，跨度 10 天，月数 1。
        var records = new List<LootRecord>
        {
            Record(new DateOnly(2026, 1, 1), ("沈没船の指輪", 8000, 1)),
            Record(new DateOnly(2026, 1, 10), ("沈没船の指輪", 8000, 1)),
        };

        LootStatisticsSnapshot stats = LootStatistics.Compute(records);

        Assert.Equal(16000, stats.TotalValue);
        Assert.Equal(10, stats.OperatingDays);
        Assert.Equal(1600, stats.AveragePerDay);
        Assert.Equal(16000, stats.AveragePerMonth);
        Assert.Equal(16000, stats.AveragePerYear);
    }

    [Fact]
    public void Compute_CountsMonthsAndYearsAcrossBoundaries()
    {
        var records = new List<LootRecord>
        {
            Record(new DateOnly(2025, 11, 20), ("沈没船の指輪", 8000, 1)),
            Record(new DateOnly(2026, 2, 5), ("沈没船の指輪", 8000, 1)),
        };

        LootStatisticsSnapshot stats = LootStatistics.Compute(records);

        // 2025-11 到 2026-02 是 4 个日历月，跨 2 个日历年。
        Assert.Equal(16000 / 4.0, stats.AveragePerMonth, precision: 6);
        Assert.Equal(8000, stats.AveragePerYear);
    }

    [Fact]
    public void Compute_MergesHighQualityAndNormalQualityUnderOneItem()
    {
        var record = new LootRecord
        {
            Date = new DateOnly(2026, 10, 27),
            Items =
            [
                new LootItem { ItemId = 5189, ItemName = "エメラルド", Quantity = 29, IsHighQuality = true, UnitPrice = 3 },
                new LootItem { ItemId = 5189, ItemName = "エメラルド", Quantity = 27, IsHighQuality = false, UnitPrice = 2 },
                new LootItem { ItemId = 22507, ItemName = "沈没船の高級首飾り", Quantity = 5, UnitPrice = 34500 },
            ],
        };

        LootStatisticsSnapshot stats = LootStatistics.Compute([record]);

        LootStatisticsItem gem = stats.ItemsByValue.Single(x => x.ItemName == "エメラルド");
        Assert.Equal(56, gem.Quantity);
        Assert.Equal(141, gem.TotalValue);

        // 按收益降序：项链在前。
        Assert.Equal("沈没船の高級首飾り", stats.ItemsByValue[0].ItemName);
        Assert.Equal(100.0 * 141 / 172641, gem.SharePercent, precision: 6);
        Assert.Equal(100, stats.ItemsByValue.Sum(x => x.SharePercent), precision: 6);
    }

    [Fact]
    public void Compute_SplitsTotalsByCharacter()
    {
        Guid alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var records = new List<LootRecord>
        {
            new() { CharacterId = alice, Date = new DateOnly(2026, 10, 27), Items = [new LootItem { ItemName = "エメラルド", Quantity = 10, UnitPrice = 2 }] },
            new() { CharacterId = bob, Date = new DateOnly(2026, 10, 27), Items = [new LootItem { ItemName = "エメラルド", Quantity = 30, UnitPrice = 2 }] },
            new() { CharacterId = bob, Date = new DateOnly(2026, 10, 28), Items = [new LootItem { ItemName = "エメラルド", Quantity = 40, UnitPrice = 2 }] },
        };

        LootStatisticsSnapshot stats = LootStatistics.Compute(records);

        Assert.Equal(2, stats.ByCharacter.Count);
        Assert.Equal(bob, stats.ByCharacter[0].CharacterId);
        Assert.Equal(140, stats.ByCharacter[0].TotalValue);
        Assert.Equal(2, stats.ByCharacter[0].RecordCount);
        Assert.Equal(20, stats.ByCharacter[1].TotalValue);
        Assert.Equal(100, stats.ByCharacter.Sum(x => x.SharePercent), precision: 6);
    }

    [Fact]
    public void Compute_SplitsTotalsBySubmarineAndKeepsUnattributedLoot()
    {
        Guid first = Guid.Parse("44444444-4444-4444-4444-444444444441");
        Guid second = Guid.Parse("44444444-4444-4444-4444-444444444442");

        var records = new List<LootRecord>
        {
            new()
            {
                Date = new DateOnly(2026, 10, 27),
                SubmarineId = first,
                Items = [new LootItem { ItemName = "エメラルド", Quantity = 10, UnitPrice = 2 }],
            },
            new()
            {
                Date = new DateOnly(2026, 10, 27),
                SubmarineId = second,
                Items = [new LootItem { ItemName = "エメラルド", Quantity = 40, UnitPrice = 2 }],
            },
            new()
            {
                Date = new DateOnly(2026, 10, 27),
                Items = [new LootItem { ItemName = "エメラルド", Quantity = 5, UnitPrice = 2 }],
            },
        };

        LootStatisticsSnapshot stats = LootStatistics.Compute(records);

        Assert.Equal(3, stats.BySubmarine.Count);
        Assert.Equal(second, stats.BySubmarine[0].SubmarineId);
        Assert.Equal(80, stats.BySubmarine[0].TotalValue);
        // 没有艇归属的旧记录也要出现，不能被静默丢掉。
        Assert.Contains(stats.BySubmarine, x => x.SubmarineId is null && x.TotalValue == 10);
    }

    private static LootRecord Record(DateOnly date, params (string Name, int UnitPrice, int Quantity)[] items)
    {
        return new LootRecord
        {
            Date = date,
            Items =
            [
                .. items.Select(x => new LootItem
                {
                    ItemName = x.Name,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                }),
            ],
        };
    }
}
