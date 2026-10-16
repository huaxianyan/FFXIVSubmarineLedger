namespace FFXIVSubLedger.Domain.Catalog;

/// <summary>
/// 同一个图标可能对应多个道具。这里记住用户上次的选择，
/// 下次遇到同一图标时把它排在候选最前面。
/// </summary>
public sealed class IconChoice
{
    public uint IconId { get; set; }

    public uint ItemId { get; set; }

    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.Now;
}
