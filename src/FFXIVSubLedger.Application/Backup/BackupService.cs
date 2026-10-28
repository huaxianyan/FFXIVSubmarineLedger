using FFXIVSubLedger.Application.Localization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Errors;

namespace FFXIVSubLedger.Application.Backup;

/// <summary>
/// 备份的导出与导入。文件是明文本 JSON，版本不认识就拒绝——
/// 猜版本号去迁最容易把用户数据迁坏。
/// </summary>
public sealed class BackupService(IBackupStore store)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public async Task<BackupSummary> ExportAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        BackupDocument document = await store.ExportAsync(ct).ConfigureAwait(false);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, ct).ConfigureAwait(false);

        return Summarize(document);
    }

    /// <summary>
    /// 读入备份并整体替换当前数据。文件不是合法备份、版本不支持、或记录指向不存在的角色时，
    /// 都不动现有数据。
    /// </summary>
    public async Task<BackupSummary> ImportAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new ValidationException(Loc.Format("validation.backupMissing", path));

        BackupDocument? document;
        try
        {
            await using FileStream stream = File.OpenRead(path);
            document = await JsonSerializer
                .DeserializeAsync<BackupDocument>(stream, SerializerOptions, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            throw new ValidationException(Loc.Get("validation.backupInvalid"));
        }

        if (document is null)
            throw new ValidationException(Loc.Get("validation.backupEmpty"));

        if (document.Version != BackupDocument.CurrentVersion)
            throw new ValidationException(Loc.Format("validation.backupVersion", document.Version, BackupDocument.CurrentVersion));

        HashSet<Guid> characterIds = [.. document.Characters.Select(x => x.Id)];
        Guid? orphan = document.Records
            .Where(x => !characterIds.Contains(x.CharacterId))
            .Select(x => (Guid?)x.CharacterId)
            .FirstOrDefault();

        if (orphan is not null)
            throw new ValidationException(Loc.Format("validation.backupOrphanCharacter", orphan));

        HashSet<Guid> submarineIds = [.. document.Submarines.Select(x => x.Id)];
        Guid? orphanSubmarine = document.Records
            .Where(x => x.SubmarineId is { } id && !submarineIds.Contains(id))
            .Select(x => x.SubmarineId)
            .FirstOrDefault();

        if (orphanSubmarine is not null)
            throw new ValidationException(Loc.Format("validation.backupOrphanSubmarine", orphanSubmarine));

        await store.ImportAsync(document, ct).ConfigureAwait(false);

        return Summarize(document);
    }

    private static BackupSummary Summarize(BackupDocument document) => new(
        document.Characters.Count,
        document.FreeCompanies.Count,
        document.Submarines.Count,
        document.Records.Count,
        document.Records.Sum(x => x.Items.Count));
}
