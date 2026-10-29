namespace FFXIVSubmarineLedger.Domain.Records;

/// <summary>收益记录里的一条道具明细。</summary>
public sealed class LootItem
{
    public uint ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>是否为 HQ 品。HQ 与 NQ 卖店价不同，落库不能把两者合并。</summary>
    public bool IsHighQuality { get; set; }

    /// <summary>单位卖店价。来自掉落候选表，不由用户手填（除非是词表里没有的物名）。</summary>
    public long UnitPrice { get; set; }

    /// <summary>候选表里没有的物名，由用户手工录入名称与价格。</summary>
    public bool IsUnknown { get; set; }

    /// <summary>小计。用 long：累计到亿级时 int 会溢出（一年就能到 3.6 亿）。</summary>
    public long Subtotal => (long)Quantity * UnitPrice;
}
