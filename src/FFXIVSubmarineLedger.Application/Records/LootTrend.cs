using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Application.Records;

/// <summary>趋势图的时间粒度。</summary>
public enum TrendGranularity
{
    /// <summary>按自然日。</summary>
    Day,

    /// <summary>按日历月。</summary>
    Month,
}

/// <summary>趋势图上的一个点。</summary>
/// <param name="Period">该点代表的日子或月份（月粒度取当月 1 日）。</param>
/// <param name="TotalValue">该区间的收益合计。</param>
public sealed record TrendPoint(DateOnly Period, long TotalValue);

/// <summary>
/// 趋势序列。中间没有记录的区间会补 0——不补的话折线会把「那天没收益」
/// 画成「从上一笔直接连到下一笔」，看起来像连续产出，与事实不符。
/// </summary>
/// <param name="Granularity">粒度。</param>
/// <param name="Points">从最早到最晚，无缺口的点序列。</param>
public sealed record TrendSeries(TrendGranularity Granularity, IReadOnlyList<TrendPoint> Points)
{
    public long MaxValue => Points.Count == 0 ? 0 : Points.Max(x => x.TotalValue);
}

public static class LootTrend
{
    public static TrendSeries Compute(IReadOnlyList<LootRecord> records, TrendGranularity granularity)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
            return new TrendSeries(granularity, []);

        DateOnly first = records.Min(x => x.Date);
        DateOnly last = records.Max(x => x.Date);

        Dictionary<DateOnly, long> totals = records
            .GroupBy(x => Key(x.Date, granularity))
            .ToDictionary(group => group.Key, group => group.Sum(x => x.TotalValue));

        var points = new List<TrendPoint>();
        foreach (DateOnly period in Periods(first, last, granularity))
        {
            points.Add(new TrendPoint(period, totals.GetValueOrDefault(period)));
        }

        return new TrendSeries(granularity, points);
    }

    private static DateOnly Key(DateOnly date, TrendGranularity granularity) =>
        granularity == TrendGranularity.Day ? date : new DateOnly(date.Year, date.Month, 1);

    private static IEnumerable<DateOnly> Periods(DateOnly first, DateOnly last, TrendGranularity granularity)
    {
        DateOnly current = Key(first, granularity);
        DateOnly end = Key(last, granularity);

        while (current <= end)
        {
            yield return current;
            current = granularity == TrendGranularity.Day
                ? current.AddDays(1)
                : current.AddMonths(1);
        }
    }
}
