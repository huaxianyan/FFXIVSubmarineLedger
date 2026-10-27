namespace FFXIVSubLedger.Infrastructure.Parsing;

/// <summary>
/// 从一批聊天行里挑出「这次才出现」的行。
///
/// 不能简单地按「这句话见过没有」判断：同一种道具会分两行入包，日志里就是两条一模一样的
/// 「エメラルド×25を入手した。」。所以按出现次数比对——这一批里出现第 N 次，只有当 N
/// 超过此前已处理的次数时才算新。
/// </summary>
public sealed class LineDeduplicator
{
    private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

    public IReadOnlyList<string> TakeNew(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var fresh = new List<string>();

        foreach (IGrouping<string, string> group in lines.GroupBy(x => x, StringComparer.Ordinal))
        {
            int alreadySeen = _seen.GetValueOrDefault(group.Key);
            int count = group.Count();
            _seen[group.Key] = Math.Max(alreadySeen, count);

            for (int index = alreadySeen; index < count; index++)
                fresh.Add(group.Key);
        }

        return fresh;
    }
}
