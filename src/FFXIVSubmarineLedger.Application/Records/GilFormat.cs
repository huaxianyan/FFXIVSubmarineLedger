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
    /// <summary>完整写法：千分位 + 单位。<c>995,392 Gil</c>。</summary>
    public static string Full(long value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    public static string Full(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    /// <summary>
    /// 紧凑写法：一万以下原样，一万以上用「万」，一亿以上用「亿 / 億」。
    /// <c>363,215,608</c> → <c>3.6 亿 Gil</c>。
    /// </summary>
    public static string Compact(long value)
    {
        long magnitude = Math.Abs(value);

        if (magnitude < 10_000)
            return Full(value);

        // 用「万」会显示成 "10000 万" 的时候（99,995,000 以上）宁可进位到「亿」，
        // 否则量级读起来是错的。
        if (magnitude < 99_995_000)
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{value / 10_000.0:0.#} {Loc.Get("money.tenThousand")} Gil");

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{value / 100_000_000.0:0.##} {Loc.Get("money.hundredMillion")} Gil");
    }
}
