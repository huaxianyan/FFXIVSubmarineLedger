using System.Globalization;
using FFXIVSubmarineLedger.Application.Localization;

namespace FFXIVSubmarineLedger.Application.Records;

/// <summary>
/// 金额显示。收艇一年就能到三亿多，全部写出来是「363,215,608 Gil」，
/// 卡片上读不出量级，所以要给一个紧凑写法：中文用「万 / 亿」，日文用「万 / 億」。
///
/// 两处都保留：完整写法是账本要的精度，紧凑写法是给人看的量级。
/// </summary>
public static class GilFormat
{
    private const long Unit = 10_000;

    /// <summary>到一亿才换单位，没到就是「万」。</summary>
    private const long HundredMillion = 100_000_000;

    /// <summary>完整写法：千分位 + 单位。<c>995,392 Gil</c>。</summary>
    public static string Full(long value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    public static string Full(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    /// <summary>
    /// 紧凑写法：一万以下原样，一万以上用「万」，**满一亿才**换「亿 / 億」。
    /// 「万」保留一位小数、「亿」两位：再精细就该看完整写法了，多出来的位数只会让卡片更难读。
    /// </summary>
    public static string Compact(long value) => Compact(value, 0.1, 0.01);

    /// <summary>只要数值的紧凑写法，不带 <c>Gil</c>：给图表坐标轴这种地方用。</summary>
    public static string CompactNumber(long value)
    {
        long magnitude = Math.Abs(value);

        if (magnitude < Unit)
            return value.ToString("N0", CultureInfo.InvariantCulture);

        return magnitude < HundredMillion
            ? string.Create(CultureInfo.InvariantCulture, $"{Trim(value / (double)Unit, 0.1):0.#} {Loc.Get("money.tenThousand")}")
            : string.Create(CultureInfo.InvariantCulture, $"{Trim(value / (double)HundredMillion, 0.01):0.##} {Loc.Get("money.hundredMillion")}");
    }

    /// <summary>
    /// 紧凑写法，可指定两个量级的保留位数。
    /// </summary>
    public static string Compact(long value, double tenThousand, double hundredMillion)
    {
        long magnitude = Math.Abs(value);

        if (magnitude < Unit)
            return Full(value);

        if (magnitude < HundredMillion)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{Trim(value / (double)Unit, tenThousand):0.#} {Loc.Get("money.tenThousand")} Gil");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Trim(value / (double)HundredMillion, hundredMillion):0.##} {Loc.Get("money.hundredMillion")} Gil");
    }

    /// <summary>
    /// 按位数截断而不是四舍五入：四舍五入会把 99,999,999 写成「10000 万」，
    /// 那既不是一万万也不是一亿，量级读起来是错的。宁可少显示一位。
    /// </summary>
    private static double Trim(double value, double step)
    {
        double factor = 1 / step;
        return Math.Truncate(value * factor) / factor;
    }
}
