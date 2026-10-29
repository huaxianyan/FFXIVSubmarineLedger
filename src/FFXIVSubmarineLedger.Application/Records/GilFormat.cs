using System.Globalization;
using FFXIVSubmarineLedger.Application.Localization;

namespace FFXIVSubmarineLedger.Application.Records;

/// <summary>
/// 金额显示。主显示是**带单位的概数**（<c>173 万 Gil</c>），只看量级，不保留小数；
/// 精确值（<c>1,730,883 Gil</c>）作副行，要核对时再看。
///
/// 单位：一万以上用「万」，满一亿才用「亿 / 億」。
/// </summary>
public static class GilFormat
{
    private const long Unit = 10_000;

    /// <summary>到一亿才换单位，没到就是「万」。</summary>
    private const long HundredMillion = 100_000_000;

    /// <summary>精确写法：千分位 + 单位。给「核对」用的副行。<c>995,392 Gil</c>。</summary>
    public static string Full(long value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    public static string Full(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    /// <summary>
    /// 概数写法，主显示用：<c>363,215,608</c> → <c>3 亿 Gil</c>，<c>995,392</c> → <c>99 万 Gil</c>。
    /// 一万以下原样（那时本来就很短）。最多一位小数。
    /// </summary>
    public static string Compact(long value)
    {
        long magnitude = Math.Abs(value);

        if (magnitude < Unit)
            return Full(value);

        return magnitude < HundredMillion
            ? string.Create(CultureInfo.InvariantCulture, $"{Trim(magnitude / (double)Unit):0.#} {Loc.Get("money.tenThousand")} Gil")
            : string.Create(CultureInfo.InvariantCulture, $"{Trim(magnitude / (double)HundredMillion):0.#} {Loc.Get("money.hundredMillion")} Gil");
    }

    /// <summary>只要数值的概数写法，不带 <c>Gil</c>：给图表坐标轴这种地方用。</summary>
    public static string CompactNumber(long value)
    {
        long magnitude = Math.Abs(value);

        if (magnitude < Unit)
            return value.ToString("N0", CultureInfo.InvariantCulture);

        return magnitude < HundredMillion
            ? string.Create(CultureInfo.InvariantCulture, $"{Trim(magnitude / (double)Unit):0.#} {Loc.Get("money.tenThousand")}")
            : string.Create(CultureInfo.InvariantCulture, $"{Trim(magnitude / (double)HundredMillion):0.#} {Loc.Get("money.hundredMillion")}");
    }

    /// <summary>概数与精确值是否不同。相同（一万以下）就不用显示副行。</summary>
    public static bool HasDetail(long value) => Math.Abs(value) >= Unit;

    /// <summary>
    /// 截断到一位小数，不四舍五入：否则 99,999,999 会变成「10000 万」，
    /// 那既不是一万万也不是一亿，量级是错的。
    /// </summary>
    private static double Trim(double value) => Math.Truncate(value * 10) / 10;
}
