using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Application.Records;

/// <summary>按道具汇总后的占比行。</summary>
/// <param name="ItemId">道具 ID。词表外的物名为 0。</param>
/// <param name="ItemName">落库时记下的道具名（日志语言），显示时按界面语言换算。</param>
/// <param name="Quantity">总数量，HQ 与 NQ 合并计。</param>
/// <param name="TotalValue">总收益。</param>
/// <param name="SharePercent">占总收益的百分比，0–100。</param>
public sealed record LootStatisticsItem(uint ItemId, string ItemName, int Quantity, int TotalValue, double SharePercent);

/// <summary>按角色拆分后的收益占比。</summary>
/// <param name="CharacterId">角色 ID。</param>
/// <param name="RecordCount">该角色的收艇次数。</param>
/// <param name="TotalValue">该角色的总收益。</param>
/// <param name="SharePercent">占总收益的百分比，0–100。</param>
public sealed record LootStatisticsCharacter(Guid CharacterId, int RecordCount, int TotalValue, double SharePercent);

/// <summary>按艇拆分后的收益占比。</summary>
/// <param name="SubmarineId">艇 ID。日志里没等到归属的收益为 null。</param>
/// <param name="RecordCount">该艇的收艇次数。</param>
/// <param name="TotalValue">该艇的总收益。</param>
/// <param name="SharePercent">占总收益的百分比，0–100。</param>
public sealed record LootStatisticsSubmarine(Guid? SubmarineId, int RecordCount, int TotalValue, double SharePercent);

/// <summary>统计页要用的所有数。</summary>
/// <param name="TotalValue">历史总收益。</param>
/// <param name="RecordCount">收艇次数，即记录条数。</param>
/// <param name="ItemCount">不同道具种类数。</param>
/// <param name="OperatingDays">运营天数，按最早到最晚记录的自然日跨度算，含首尾。</param>
/// <param name="FirstDate">最早记录日期。</param>
/// <param name="LastDate">最晚记录日期。</param>
/// <param name="AveragePerDay">日均 = 总收益 ÷ 运营天数。</param>
/// <param name="AveragePerMonth">月均 = 总收益 ÷ 跨越的日历月数。</param>
/// <param name="AveragePerYear">年均 = 总收益 ÷ 跨越的日历年数。</param>
/// <param name="ItemsByValue">按收益降序的道具占比。</param>
/// <param name="ByCharacter">按收益降序的角色占比。</param>
/// <param name="BySubmarine">按收益降序的艇占比。</param>
public sealed record LootStatisticsSnapshot(
    int TotalValue,
    int RecordCount,
    int ItemCount,
    int OperatingDays,
    DateOnly? FirstDate,
    DateOnly? LastDate,
    double AveragePerDay,
    double AveragePerMonth,
    double AveragePerYear,
    IReadOnlyList<LootStatisticsItem> ItemsByValue,
    IReadOnlyList<LootStatisticsCharacter> ByCharacter,
    IReadOnlyList<LootStatisticsSubmarine> BySubmarine);

/// <summary>
/// 统计口径。这里只用已经落库的记录，不含未确认的截图。
///
/// 日均按自然日跨度算（最早到最晚，含首尾），不再单独剔除没记录的日子——
/// 收艇本身就是每天收一次，空着的日子就是没运营或漏记，剔除会把日均抬高。
/// </summary>
public static class LootStatistics
{
    public static LootStatisticsSnapshot Compute(IReadOnlyList<LootRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
            return new LootStatisticsSnapshot(0, 0, 0, 0, null, null, 0, 0, 0, [], [], []);

        int total = records.Sum(x => x.TotalValue);
        DateOnly first = records.Min(x => x.Date);
        DateOnly last = records.Max(x => x.Date);

        int operatingDays = last.DayNumber - first.DayNumber + 1;
        int months = ((last.Year * 12) + last.Month) - ((first.Year * 12) + first.Month) + 1;
        int years = last.Year - first.Year + 1;

        // 按 ItemId 汇总：同一个道具在不同语言下写法不同，名字不能当键。
        var items = records
            .SelectMany(x => x.Items)
            .GroupBy(x => x.ItemId != 0 ? $"i:{x.ItemId}" : $"u:{x.ItemName}", StringComparer.Ordinal)
            .Select(group => new LootStatisticsItem(
                group.First().ItemId,
                group.First().ItemName,
                group.Sum(x => x.Quantity),
                group.Sum(x => x.Subtotal),
                total == 0 ? 0 : group.Sum(x => x.Subtotal) * 100.0 / total))
            .OrderByDescending(x => x.TotalValue)
            .ThenBy(x => x.ItemName, StringComparer.Ordinal)
            .ToList();

        var byCharacter = records
            .GroupBy(x => x.CharacterId)
            .Select(group => new LootStatisticsCharacter(
                group.Key,
                group.Count(),
                group.Sum(x => x.TotalValue),
                total == 0 ? 0 : group.Sum(x => x.TotalValue) * 100.0 / total))
            .OrderByDescending(x => x.TotalValue)
            .ToList();

        var bySubmarine = records
            .GroupBy(x => x.SubmarineId)
            .Select(group => new LootStatisticsSubmarine(
                group.Key,
                group.Count(),
                group.Sum(x => x.TotalValue),
                total == 0 ? 0 : group.Sum(x => x.TotalValue) * 100.0 / total))
            .OrderByDescending(x => x.TotalValue)
            .ToList();

        return new LootStatisticsSnapshot(
            total,
            records.Count,
            items.Count,
            operatingDays,
            first,
            last,
            (double)total / operatingDays,
            (double)total / months,
            (double)total / years,
            items,
            byCharacter,
            bySubmarine);
    }
}
