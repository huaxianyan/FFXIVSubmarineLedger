namespace FFXIVSubmarineLedger.Application.Backup;

/// <summary>备份里的角色。</summary>
public sealed record BackupCharacter(
    Guid Id,
    string Name,
    string World,
    bool IsActive,
    bool IsDefault,
    DateTimeOffset CreatedAt);

/// <summary>备份里的部队。</summary>
public sealed record BackupFreeCompany(
    Guid Id,
    string Name,
    Guid CharacterId,
    DateTimeOffset CreatedAt);

/// <summary>备份里的一艘潜水艦。</summary>
public sealed record BackupSubmarine(
    Guid Id,
    Guid CharacterId,
    string Name,
    DateTimeOffset CreatedAt);

/// <summary>备份里的一条道具明细。</summary>
public sealed record BackupLootItem(
    uint ItemId,
    string ItemName,
    int Quantity,
    bool IsHighQuality,
    int UnitPrice,
    bool IsUnknown);

/// <summary>备份里的一笔收艇记录。</summary>
public sealed record BackupLootRecord(
    Guid Id,
    Guid CharacterId,
    Guid? FreeCompanyId,
    Guid? SubmarineId,
    DateOnly Date,
    DateTimeOffset CreatedAt,
    string Note,
    IReadOnlyList<BackupLootItem> Items);

/// <summary>
/// 备份文件的内容。JSON 明文，用户能直接看、能自己校验，不需要本程序也能读懂。
/// </summary>
/// <param name="Version">格式版本。导入时不认识就拒绝，不猜。</param>
/// <param name="ExportedAt">导出时间。</param>
public sealed record BackupDocument(
    int Version,
    DateTimeOffset ExportedAt,
    IReadOnlyList<BackupCharacter> Characters,
    IReadOnlyList<BackupFreeCompany> FreeCompanies,
    IReadOnlyList<BackupSubmarine> Submarines,
    IReadOnlyList<BackupLootRecord> Records)
{
    /// <summary>2：加入潜水艦层级与记录的艇归属。</summary>
    public const int CurrentVersion = 2;
}

/// <summary>导入结果摘要，用于给用户一句人话。</summary>
public sealed record BackupSummary(int Characters, int FreeCompanies, int Submarines, int Records, int Items);
