using FFXIVSubmarineLedger.Domain.Texts;

namespace FFXIVSubmarineLedger.Application.Records;

/// <summary>OCR 读出的艇名与已有艇名的关系。</summary>
public enum SubmarineNameKind
{
    /// <summary>日志里没有艇名（没等到「探索完了」那行）。</summary>
    Unmarked,

    /// <summary>和已有的艇一模一样。</summary>
    Exact,

    /// <summary>读歪了，但只有一艘艇足够近，就按它记。</summary>
    Corrected,

    /// <summary>有点像但不敢替人定（两艘一样近，或者只差到「可能」的程度），交给人选。</summary>
    Ambiguous,

    /// <summary>附近没有已有的艇，当成一艘新艇。</summary>
    New,
}

/// <param name="Name">对上的艇名；对不上或分不清时为 <c>null</c>。</param>
/// <param name="Kind">怎么判断出来的。</param>
/// <param name="NearNames">像的已有艇名（按远近排序），给「请选一下」这类提示用。</param>
public readonly record struct SubmarineNameGuess(
    string? Name,
    SubmarineNameKind Kind,
    IReadOnlyList<string> NearNames);

/// <summary>
/// 把 OCR 读出来的艇名对回角色名下已有的艇。
///
/// 认字认歪会凭空造出一艘艇：实测 2026-11-22 的一次截图把 Strawberry / Blackberry /
/// Blueberry 读成 Strawberrv / Blackberrvy / Blug5erry，一落库就多了三艘各一条记录的艇，
/// 按艇统计从此对不上。艇名本身只有玩家自己起的那几个，所以宁可对回去也不要新建。
///
/// 两档阈值，都按名字长度缩（名字越短，能容忍的差异越少）：
/// 一档是「可以直接改写成这艘艇」，只有唯一的一艘落在这档里才改写；
/// 另一档宽一个字符，只用来给出候选让人自己挑——落在这一档的既不改写也不新建。
/// 这样既不会把真新买的艇并进老艇，也不会因为读歪一个字就多出一艘艇。
/// </summary>
public static class SubmarineNameMatcher
{
    /// <summary>可以直接改写的最多差异（长名字的上限）。</summary>
    public const int AutoDistance = 2;

    /// <param name="read">OCR 读出来的艇名，可以是 <c>null</c> 或空串。</param>
    /// <param name="known">该角色名下已有的艇名。</param>
    public static SubmarineNameGuess Guess(string? read, IReadOnlyList<string> known)    {
        ArgumentNullException.ThrowIfNull(known);

        if (string.IsNullOrEmpty(read))
            return new SubmarineNameGuess(null, SubmarineNameKind.Unmarked, []);

        if (known.Contains(read, StringComparer.Ordinal))
            return new SubmarineNameGuess(read, SubmarineNameKind.Exact, []);

        List<(string Name, int Distance)> scored =
        [
            .. known
                .Select(name => (Name: name, Distance: TextDistance.Levenshtein(read, name)))
                .Where(x => x.Distance <= HintLimit(x.Name))
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Name, StringComparer.Ordinal),
        ];

        if (scored.Count == 0)
            return new SubmarineNameGuess(null, SubmarineNameKind.New, []);

        int best = scored[0].Distance;
        List<string> nearest = [.. scored.Where(x => x.Distance == best).Select(x => x.Name)];

        if (nearest.Count == 1 && best <= AutoLimit(nearest[0]))
            return new SubmarineNameGuess(nearest[0], SubmarineNameKind.Corrected, []);

        return new SubmarineNameGuess(
            null,
            SubmarineNameKind.Ambiguous,
            [.. scored.Select(x => x.Name)]);
    }

    /// <summary>
    /// 可以直接改写到这艘艇的差异上限。8 字以下根本不自动改写：FFXIV 里「Sub1」「Sub2」
    /// 这种编号命名很常见，差一个字就可能真是另一艘艇，宁可让人来选。
    /// </summary>
    private static int AutoLimit(string name) => name.Length >= 8 ? Math.Min(AutoDistance, name.Length / 4) : 0;

    /// <summary>只用来给候选的差异上限，比自动改写宽一档。</summary>
    private static int HintLimit(string name) => name.Length >= 6 ? Math.Min(3, name.Length / 3) : 1;
}
