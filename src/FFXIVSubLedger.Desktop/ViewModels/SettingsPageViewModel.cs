using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Backup;
using FFXIVSubLedger.Application.Errors;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Application.Settings;
using FFXIVSubLedger.Domain.Settings;
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
    SettingsService settings,
    string dataDirectory,
    string databasePath,
    Lazy<ItemCatalog> catalog) : ObservableObject
{
    /// <summary>
    /// 可选的显示语言，直接来自随程序分发的语言资源：加一门语言只需加一个资源，
    /// 这里不用动。语言只影响界面与道具名，不影响收集。
    /// </summary>
    public IReadOnlyList<LanguageChoice> Languages { get; } =
    [
        .. Loc.Available.Select(x => new LanguageChoice(x.Tag, x.Name)),
    ];

    [ObservableProperty]
    private LanguageChoice? _selectedLanguage = null;

    /// <summary>页面第一次显示时把当前语言对上下拉。</summary>
    public void EnsureLanguageSelected()
        => SelectedLanguage ??= Languages.FirstOrDefault(x => x.Tag == Loc.Instance.Language) ?? Languages[0];

    partial void OnSelectedLanguageChanged(LanguageChoice? value)
    {
        if (value is null || value.Tag == Loc.Instance.Language || !Loc.Instance.SetLanguage(value.Tag))
            return;

        _ = settings.SetUiLanguageAsync(value.Tag);
        StatusMessage = Loc.Get("settings.languageChanged");
    }

    public string DataDirectory { get; } = dataDirectory;

    public string DatabasePathLabel { get; } = databasePath;

    [ObservableProperty]
    private string _catalogLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    public bool HasStatus => StatusMessage.Length > 0;

    /// <summary>页面第一次显示时调用，避免在构造函数里读文件。</summary>
    public void EnsureCatalogLoaded()
    {
        CatalogLabel = Loc.Get("settings.catalogLoading");
        LoadCatalogLabel();
    }

    /// <summary>换语言后刷新页内由代码拼出来的文字。</summary>
    public void Relocalize()
    {
        EnsureLanguageSelected();
        LoadCatalogLabel();
    }

    private void LoadCatalogLabel()
    {
        try
        {
            CatalogLabel = Loc.Format("settings.catalog", catalog.Value.Entries.Count, ItemCatalog.DefaultPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException)
        {
            CatalogLabel = Loc.Format("settings.catalogFailed", exception.Message);
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            WindowsShell.OpenFolder(DataDirectory);
            StatusMessage = Loc.Get("settings.opened");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("settings.openFailed", exception.Message);
        }
    }

    [RelayCommand]
    private async Task ExportBackupAsync()
    {
        if (topLevelProvider() is not { } topLevel)
        {
            StatusMessage = Loc.Get("settings.pickerUnavailable");
            return;
        }

        try
        {
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.Get("settings.exportTitle"),
                SuggestedFileName = string.Create(
                    CultureInfo.InvariantCulture,
                    $"subledger-backup-{DateTime.Now:yyyyMMdd-HHmm}.json"),
                FileTypeChoices = [BackupFileType],
            }).ConfigureAwait(true);

            if (file is null)
                return;

            string path = file.Path.LocalPath;
            BackupSummary summary = await backup.ExportAsync(path).ConfigureAwait(true);

            StatusMessage = Loc.Format("settings.exported", path, summary.Characters, summary.Records, summary.Items);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("settings.exportFailed", exception.Message);
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
            StatusMessage = Loc.Get("settings.pickerUnavailable");
            return;
        }

        try
        {
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Loc.Get("settings.importTitle"),
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

            StatusMessage = Loc.Format("settings.imported", summary.Characters, summary.Records, safetyPath);
        }
        catch (ValidationException exception)
        {
            StatusMessage = Loc.Format("settings.importRejected", exception.Message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("settings.importFailed", exception.Message);
        }
    }

    private static FilePickerFileType BackupFileType => new(Loc.Get("settings.backupFileType"))
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };
}
