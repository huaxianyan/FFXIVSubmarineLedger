using FFXIVSubmarineLedger.Domain.Texts;

namespace FFXIVSubmarineLedger.Application.Names;

/// <summary>OCR 读出来的名字与已有名字的关系。</summary>
public enum NameMatchKind
{
    /// <summary>日志里根本没有这个名字。</summary>
    Missing,

    /// <summary>和已有的名字一模一样。</summary>
    Exact,

    /// <summary>读歪了，但只有一个名字足够近，就按它记。</summary>
    Corrected,

    /// <summary>有点像但不敢替人定（几个一样近，或者只差到「可能」的程度），交给人选。</summary>
    Ambiguous,

    /// <summary>附近没有已有的名字，当成一个新的。</summary>
    New,
}

/// <param name="Name">对上的名字；对不上或分不清时为 <c>null</c>。</param>
/// <param name="Kind">怎么判断出来的。</param>
/// <param name="NearNames">像的已有名字（按远近排序），给「请选一下」这类提示用。</param>
public readonly record struct NameGuess(string? Name, NameMatchKind Kind, IReadOnlyList<string> NearNames);

/// <summary>
/// 把 OCR 读出来的名字对回已有的名字。艇名和角色名都走这一套。
///
/// 认字认歪会凭空造出一个新对象：实测一次截图把艇名 Strawberry / Blackberry /
/// Blueberry 读成 Strawberrv / Blackberrvy / Blug5erry，落库就多出三艘艇；角色名同理
/// （同一批行里「Tiina Branford」被读成「Tilna Branford」过）。这些名字都是玩家自己
/// 起的、数量很有限，所以宁可对回去也不要新建。
///
/// 两档阈值，都按名字长度缩（名字越短，能容忍的差异越少）：
/// 一档是「可以直接改写成这个名字」，只有唯一一个落在这一档里才改写；
/// 另一档宽一档，只用来给出候选让人自己挑——落在这一档的既不改写也不新建。
/// 这样既不会把真正新出现的名字并进老的，也不会因为读歪一个字就多出一个。
/// </summary>
public static class NameMatcher
{
    /// <summary>可以直接改写的最多差异（长名字的上限）。</summary>
    public const int AutoDistance = 2;

    /// <param name="read">OCR 读出来的名字，可以是 <c>null</c> 或空串。</param>
    /// <param name="known">已有的名字。</param>
    public static NameGuess Guess(string? read, IReadOnlyList<string> known)
    {
        ArgumentNullException.ThrowIfNull(known);

        if (string.IsNullOrEmpty(read))
            return new NameGuess(null, NameMatchKind.Missing, []);

        // 识别出来的名字里常有 OCR 插进去的空格（「Black berry」），比对前先抹掉。
        string readKey = Normalize(read);

        if (known.FirstOrDefault(name => Normalize(name).Equals(readKey, StringComparison.Ordinal)) is { } same)
            return new NameGuess(same, NameMatchKind.Exact, []);

        List<(string Name, int Distance)> scored =
        [
            .. known
                .Distinct(StringComparer.Ordinal)
                .Select(name => (Name: name, Distance: TextDistance.Levenshtein(readKey, Normalize(name))))
                .Where(x => x.Distance <= HintLimit(Normalize(x.Name)))
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Name, StringComparer.Ordinal),
        ];

        if (scored.Count == 0)
            return new NameGuess(null, NameMatchKind.New, []);

        int best = scored[0].Distance;
        List<string> nearest = [.. scored.Where(x => x.Distance == best).Select(x => x.Name)];

        if (nearest.Count == 1 && best <= AutoLimit(nearest[0]))
            return new NameGuess(nearest[0], NameMatchKind.Corrected, []);

        return new NameGuess(null, NameMatchKind.Ambiguous, [.. scored.Select(x => x.Name)]);
    }

    private static string Normalize(string name) =>
        name.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("　", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// 可以直接改写到这个名字的差异上限。8 字以下根本不自动改写：FFXIV 里
    /// 「Sub1」「Sub2」这种编号命名很常见，差一个字就可能真是另一个。
    /// </summary>
    private static int AutoLimit(string name) => name.Length >= 8 ? Math.Min(AutoDistance, name.Length / 4) : 0;

    /// <summary>只用来给候选的差异上限，比自动改写宽一档。</summary>
    private static int HintLimit(string name) => name.Length >= 6 ? Math.Min(3, name.Length / 3) : 1;
}
