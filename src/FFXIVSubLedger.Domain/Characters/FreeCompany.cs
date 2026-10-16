namespace FFXIVSubLedger.Domain.Characters;

/// <summary>部队（自由会社）。潜艇归属于部队，收益再归到角色。</summary>
public sealed class FreeCompany
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid CharacterId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
