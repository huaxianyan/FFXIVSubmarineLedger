namespace FFXIVSubmarineLedger.Domain.Records;

/// <summary>一次收艇的收益记录，只按角色/部队记一笔。</summary>
public sealed class LootRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CharacterId { get; set; }

    public Guid? FreeCompanyId { get; set; }

    /// <summary>这笔收益属于哪艘艇。没有艇归属的旧记录为 null。</summary>
    public Guid? SubmarineId { get; set; }

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public string Note { get; set; } = string.Empty;

    public List<LootItem> Items { get; set; } = [];

    /// <summary>本次收益合计。价格是固定的卖店价，不需要联网查价。</summary>
    public long TotalValue => Items.Sum(x => x.Subtotal);
}
