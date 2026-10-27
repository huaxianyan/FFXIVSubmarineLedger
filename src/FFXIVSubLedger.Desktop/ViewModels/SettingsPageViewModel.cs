using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Backup;
using FFXIVSubLedger.Application.Errors;
using FFXIVSubLedger.Infrastructure.Catalog;
using FFXIVSubLedger.Platform.Windows;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 设置页。除了展示存放位置，主要作用是让用户能把数据拿出来——
/// 数据库在 AppData 里，不给导出入口等于没有备份。
/// </summary>
public sealed partial class SettingsPageViewModel(
    Func<TopLevel?> topLevelProvider,
    BackupService backup,
    string dataDirectory,
    string databasePath,
    Lazy<ItemCatalog> catalog) : ObservableObject
{
    public string DataDirectory { get; } = dataDirectory;

    public string DatabasePathLabel { get; } = databasePath;

    [ObservableProperty]
    private string _catalogLabel = "掉落表：读取中…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    public bool HasStatus => StatusMessage.Length > 0;

    /// <summary>页面第一次显示时调用，避免在构造函数里读文件。</summary>
    public void EnsureCatalogLoaded()
    {
        try
        {
            CatalogLabel = string.Create(
                CultureInfo.InvariantCulture,
                $"掉落表：{catalog.Value.Entries.Count} 条 · {ItemCatalog.DefaultPath}");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException)
        {
            CatalogLabel = $"掉落表读不到：{exception.Message}";
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            WindowsShell.OpenFolder(DataDirectory);
            StatusMessage = "已打开数据目录。";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"打不开数据目录：{exception.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportBackupAsync()
    {
        if (topLevelProvider() is not { } topLevel)
        {
            StatusMessage = "打不开文件选择器。";
            return;
        }

        try
        {
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出备份",
                SuggestedFileName = string.Create(
                    CultureInfo.InvariantCulture,
                    $"subledger-backup-{DateTime.Now:yyyyMMdd-HHmm}.json"),
                FileTypeChoices = [BackupFileType],
            }).ConfigureAwait(true);

            if (file is null)
                return;

            string path = file.Path.LocalPath;
            BackupSummary summary = await backup.ExportAsync(path).ConfigureAwait(true);

            StatusMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"已导出到 {path}：{summary.Characters} 个角色、{summary.Records} 笔记录、{summary.Items} 条明细。");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"导出失败：{exception.Message}";
        }
    }

    /// <summary>
    /// 导入是整体替换，所以先把当前数据存一份到数据目录，用户点错了还能退回来。
    /// </summary>
    [RelayCommand]
    private async Task ImportBackupAsync()
    {
        if (topLevelProvider() is not { } topLevel)
        {
            StatusMessage = "打不开文件选择器。";
            return;
        }

        try
        {
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "选择备份文件",
                    AllowMultiple = false,
                    FileTypeFilter = [BackupFileType],
                }).ConfigureAwait(true);

            if (files.Count == 0)
                return;

            string safetyPath = Path.Combine(
                DataDirectory,
                "backups",
                string.Create(CultureInfo.InvariantCulture, $"before-import-{DateTime.Now:yyyyMMdd-HHmmss}.json"));

            await backup.ExportAsync(safetyPath).ConfigureAwait(true);

            BackupSummary summary = await backup.ImportAsync(files[0].Path.LocalPath).ConfigureAwait(true);

            StatusMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"已导入 {summary.Characters} 个角色、{summary.Records} 笔记录。导入前的数据存在 {safetyPath}。");
        }
        catch (ValidationException exception)
        {
            StatusMessage = $"导入被拒绝：{exception.Message}";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"导入失败：{exception.Message}";
        }
    }

    private static FilePickerFileType BackupFileType => new("JSON 备份")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };
}
