using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

public sealed class LootRecordFactoryTests
{
    private static readonly Guid CharacterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly Date = new(2026, 10, 27);

    [Fact]
    public void Build_MergesTheSameItemAcrossLogLines()
    {
        LootRecord record = LootRecordFactory.Build(
            CharacterId,
            Date,
            [
                new LootEntryInput(22507, "沈没船の高級首飾り", 5, false, 34500, false),
                new LootEntryInput(22507, "沈没船の高級首飾り", 5, false, 34500, false),
            ]);

        LootItem item = Assert.Single(record.Items);
        Assert.Equal(10, item.Quantity);
        Assert.Equal(345000, item.Subtotal);
        Assert.Equal(345000, record.TotalValue);
    }

    [Fact]
    public void Build_KeepsHighQualityAndNormalQualityApart()
    {
        // HQ 与 NQ 单价不同，合成一条就反推不出钱数。
        LootRecord record = LootRecordFactory.Build(
            CharacterId,
            Date,
            [
                new LootEntryInput(5189, "エメラルド", 29, true, 3, false),
                new LootEntryInput(5189, "エメラルド", 29, false, 2, false),
            ]);

        Assert.Equal(2, record.Items.Count);
        Assert.Equal(87, record.Items[0].Subtotal);
        Assert.Equal(58, record.Items[1].Subtotal);
    }

    [Fact]
    public void Build_KeepsUnknownItemsSeparateByName()
    {
        // 词表外的物名没有 ItemId，全按 0 合并会把不同道具并成一条。
        LootRecord record = LootRecordFactory.Build(
            CharacterId,
            Date,
            [
                new LootEntryInput(0, "謎の巻物", 1, false, 100, true),
                new LootEntryInput(0, "別の巻物", 2, false, 50, true),
            ]);

        Assert.Equal(2, record.Items.Count);
        Assert.All(record.Items, item => Assert.True(item.IsUnknown));
        Assert.Equal(200, record.TotalValue);
    }

    [Fact]
    public void Build_SkipsRowsTheUserZeroedOut()
    {
        LootRecord record = LootRecordFactory.Build(
            CharacterId,
            Date,
            [
                new LootEntryInput(22500, "沈没船の指輪", 0, false, 8000, false),
                new LootEntryInput(23222, "海産油脂", 3, false, 1, false),
            ]);

        Assert.Equal(3, Assert.Single(record.Items).Subtotal);
    }
}
