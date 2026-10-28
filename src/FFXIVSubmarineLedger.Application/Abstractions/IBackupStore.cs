using FFXIVSubmarineLedger.Application.Backup;

namespace FFXIVSubmarineLedger.Application.Abstractions;

/// <summary>
/// 备份的读写口。导出要能把库里全部内容一次性读出来，导入要整体替换且中途失败不留半份数据。
/// </summary>
public interface IBackupStore
{
    Task<BackupDocument> ExportAsync(CancellationToken ct = default);

    Task ImportAsync(BackupDocument document, CancellationToken ct = default);
}
