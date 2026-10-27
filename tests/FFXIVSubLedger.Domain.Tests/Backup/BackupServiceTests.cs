using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Backup;
using FFXIVSubLedger.Application.Errors;

namespace FFXIVSubLedger.Domain.Tests.Backup;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "subledger-backup-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingStore _store = new();

    public BackupServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task ExportAsync_WritesTheWholeStoreToJson()
    {
        string path = Path.Combine(_directory, "backup.json");

        BackupSummary summary = await new BackupService(_store).ExportAsync(path);

        Assert.True(File.Exists(path));
        Assert.Equal(1, summary.Characters);
        Assert.Equal(1, summary.Records);
        Assert.Equal(2, summary.Items);
        Assert.Contains("\"Version\": 1", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_RejectsAnUnsupportedVersionWithoutTouchingTheStore()
    {
        string path = Path.Combine(_directory, "future.json");
        await File.WriteAllTextAsync(
            path,
            """
            { "Version": 99, "ExportedAt": "2026-10-27T00:00:00+09:00", "Characters": [], "FreeCompanies": [], "Records": [] }
            """);

        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => new BackupService(_store).ImportAsync(path));

        Assert.Contains("99", error.Message, StringComparison.Ordinal);
        Assert.False(_store.Imported);
    }

    [Fact]
    public async Task ImportAsync_RejectsARecordPointingAtAnUnknownCharacter()
    {
        string path = Path.Combine(_directory, "orphan.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "Version": 1,
              "ExportedAt": "2026-10-27T00:00:00+09:00",
              "Characters": [],
              "FreeCompanies": [],
              "Records": [
                {
                  "Id": "22222222-2222-2222-2222-222222222222",
                  "CharacterId": "33333333-3333-3333-3333-333333333333",
                  "FreeCompanyId": null,
                  "Date": "2026-10-27",
                  "CreatedAt": "2026-10-27T12:00:00+09:00",
                  "Note": "",
                  "Items": []
                }
              ]
            }
            """);

        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => new BackupService(_store).ImportAsync(path));

        Assert.Contains("不存在的角色", error.Message, StringComparison.Ordinal);
        Assert.False(_store.Imported);
    }

    [Fact]
    public async Task ImportAsync_RejectsAFileThatIsNotABackup()
    {
        string path = Path.Combine(_directory, "notes.json");
        await File.WriteAllTextAsync(path, "这不是备份");

        await Assert.ThrowsAsync<ValidationException>(() => new BackupService(_store).ImportAsync(path));
    }

    [Fact]
    public async Task ImportAsync_HandsAValidBackupToTheStore()
    {
        string path = Path.Combine(_directory, "backup.json");
        BackupService service = new(_store);
        await service.ExportAsync(path);

        BackupSummary summary = await service.ImportAsync(path);

        Assert.True(_store.Imported);
        Assert.Equal(1, summary.Records);
    }

    /// <summary>测试用的存储：导出一份固定数据，导入只记一笔账。</summary>
    private sealed class RecordingStore : IBackupStore
    {
        private static readonly Guid CharacterId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public bool Imported { get; private set; }

        public Task<BackupDocument> ExportAsync(CancellationToken ct = default) => Task.FromResult(
            new BackupDocument(
                BackupDocument.CurrentVersion,
                new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(9)),
                [new BackupCharacter(CharacterId, "Alice", "红玉海", true, true, DateTimeOffset.Now)],
                [],
                [
                    new BackupLootRecord(
                        Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        CharacterId,
                        null,
                        new DateOnly(2026, 10, 27),
                        DateTimeOffset.Now,
                        string.Empty,
                        [
                            new BackupLootItem(22507, "沈没船の高級首飾り", 5, false, 34500, false),
                            new BackupLootItem(5189, "エメラルド", 25, true, 3, false),
                        ]),
                ]));

        public Task ImportAsync(BackupDocument document, CancellationToken ct = default)
        {
            Imported = true;
            return Task.CompletedTask;
        }
    }
}
