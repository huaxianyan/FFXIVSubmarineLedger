namespace FFXIVSubLedger.Domain.Characters;

/// <summary>一个游戏角色。收益最终归属到角色。</summary>
public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string World { get; set; } = string.Empty;

    /// <summary>停用后不再出现在收艇归属的选择框里。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>只有一个角色时用于自动归属；多个角色时作为选择框的默认项。</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public string DisplayName => string.IsNullOrWhiteSpace(World) ? Name : $"{Name} @ {World}";
}
