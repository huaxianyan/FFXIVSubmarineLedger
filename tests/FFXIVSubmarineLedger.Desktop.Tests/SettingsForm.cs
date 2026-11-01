using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Backup;
using FFXIVSubmarineLedger.Application.Settings;
using FFXIVSubmarineLedger.Application.Updates;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Settings;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using FFXIVSubmarineLedger.Infrastructure.Catalog;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>搭一个不碰磁盘的设置页，供排版检查用。</summary>
internal static class SettingsForm
{
    public static SettingsPageViewModel Build()
    {
        var settings = new SettingsService(new MemoryStore());
        return new SettingsPageViewModel(
            () => null,
            new BackupService(new MemoryBackup()),
            settings,
            new UpdateService(
                new UpdateChecker("owner/repo", (_, _, _, _) => new HttpClient()),
                settings,
                new Version(0, 1, 0)),
            Path.GetTempPath(),
            Path.Combine(Path.GetTempPath(), "settings-form.db"),
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
    }

    private sealed class MemoryStore : ISettingsStore
    {
        private AppSettings _settings = new();

        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryBackup : IBackupStore
    {
        public Task<BackupDocument> ExportAsync(CancellationToken ct = default) =>
            Task.FromResult(new BackupDocument(2, DateTimeOffset.Now, [], [], [], []));

        public Task ImportAsync(BackupDocument document, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
