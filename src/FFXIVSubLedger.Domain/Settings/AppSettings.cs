namespace FFXIVSubLedger.Domain.Settings;

/// <summary>应用级设置。以 JSON 形式整体存在数据库的 settings 表里。</summary>
public sealed class AppSettings
{
    /// <summary>多角色时的默认归属角色。</summary>
    public Guid? DefaultCharacterId { get; set; }

    /// <summary>上次选择的角色，用于预选。</summary>
    public Guid? LastSelectedCharacterId { get; set; }

    /// <summary>格子边长小于该像素值时，数量交给用户手输，不做自动识别。</summary>
    public int MinCellSizeForQuantityOcr { get; set; } = 40;
}
