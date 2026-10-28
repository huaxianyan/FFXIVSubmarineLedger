namespace FFXIVSubmarineLedger.Domain.Characters;

/// <summary>
/// 一艘潜水艦。归属在角色下面——同一个角色下不同艇的收益要分开统计，
/// 而不同角色可以有同名的艇，所以艇名只在角色范围内唯一。
/// </summary>
public sealed class Submarine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CharacterId { get; set; }

    /// <summary>艇名，取自日志里的「潜水艦「…」」。</summary>
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
