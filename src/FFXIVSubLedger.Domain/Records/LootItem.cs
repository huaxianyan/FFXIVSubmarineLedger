namespace FFXIVSubLedger.Domain.Records;

/// <summary>收益记录里的一条道具明细。</summary>
public sealed class LootItem
{
    public uint ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>单位卖店价。来自掉落候选表，不由用户手填（除非是未知图标）。</summary>
    public int UnitPrice { get; set; }

    /// <summary>候选表里没有的图标，由用户手工录入名称与价格。</summary>
    public bool IsUnknown { get; set; }

    public int Subtotal => Quantity * UnitPrice;
}
