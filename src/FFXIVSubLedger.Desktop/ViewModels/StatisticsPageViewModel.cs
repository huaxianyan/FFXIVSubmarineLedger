using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Characters;
using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 收益统计页。口径全在 <see cref="LootStatistics"/> 与 <see cref="LootTrend"/> 里，
/// 这一层只负责把数摆到界面上。
/// </summary>
public sealed partial class StatisticsPageViewModel(
    ILootRecordRepository records,
    CharacterService characters,
    Func<TopLevel?> topLevelProvider) : ObservableObject
{
    private IReadOnlyList<LootRecord> _all = [];
    private IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();

    public ObservableCollection<ItemShareRowViewModel> Items { get; } = [];

    public ObservableCollection<CharacterShareRowViewModel> Characters { get; } = [];

    public ObservableCollection<RecordRowViewModel> Records { get; } = [];

    [ObservableProperty]
    private string _totalLabel = "0";

    [ObservableProperty]
    private string _averagePerDayLabel = "0";

    [ObservableProperty]
    private string _averagePerMonthLabel = "0";

    [ObservableProperty]
    private string _averagePerYearLabel = "0";

    [ObservableProperty]
    private string _spanLabel = string.Empty;

    [ObservableProperty]
    private string _itemCountLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendTitle))]
    private bool _isMonthly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecords))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    public string TrendTitle => IsMonthly ? "每月收益走势" : "每日收益走势";

    public bool HasStatus => StatusMessage.Length > 0;

    public bool HasRecords => Records.Count > 0;

    public bool IsEmpty => IsLoaded && Records.Count == 0;

    /// <summary>每日收益走势。</summary>
    [ObservableProperty]
    private TrendSeries? _trend;

    public async Task LoadAsync()
    {
        _all = await records.ListAsync().ConfigureAwait(true);
        IReadOnlyList<Character> owners = await characters.ListAsync(includeInactive: true).ConfigureAwait(true);

        _names = owners.ToDictionary(x => x.Id, x => x.DisplayName);
        LootStatisticsSnapshot stats = LootStatistics.Compute(_all);

        TotalLabel = Gil(stats.TotalValue);
        AveragePerDayLabel = Gil(stats.AveragePerDay);
        AveragePerMonthLabel = Gil(stats.AveragePerMonth);
        AveragePerYearLabel = Gil(stats.AveragePerYear);

        SpanLabel = stats.RecordCount == 0
            ? "还没有记录"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{stats.FirstDate:yyyy-MM-dd} 起 {stats.OperatingDays} 天 · {stats.RecordCount} 次收艇");

        ItemCountLabel = string.Create(CultureInfo.InvariantCulture, $"{stats.ItemCount} 种道具");

        Items.Clear();
        foreach (LootStatisticsItem item in stats.ItemsByValue)
            Items.Add(new ItemShareRowViewModel(item));

        Characters.Clear();
        foreach (LootStatisticsCharacter item in stats.ByCharacter)
            Characters.Add(new CharacterShareRowViewModel(item, NameOf(item.CharacterId)));

        Records.Clear();
        foreach (LootRecord record in _all)
            Records.Add(new RecordRowViewModel(record, NameOf(record.CharacterId), this));

        RefreshTrend();

        IsLoaded = true;
        OnPropertyChanged(nameof(HasRecords));
    }

    internal async Task DeleteAsync(RecordRowViewModel row)
    {
        await records.DeleteAsync(row.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void ShowDaily()
    {
        IsMonthly = false;
        RefreshTrend();
    }

    [RelayCommand]
    private void ShowMonthly()
    {
        IsMonthly = true;
        RefreshTrend();
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
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
                Title = "导出 CSV",
                SuggestedFileName = string.Create(
                    CultureInfo.InvariantCulture,
                    $"subledger-{DateTime.Now:yyyyMMdd-HHmm}.csv"),
                FileTypeChoices =
                [
                    new FilePickerFileType("CSV")
                    {
                        Patterns = ["*.csv"],
                        MimeTypes = ["text/csv"],
                    },
                ],
            }).ConfigureAwait(true);

            if (file is null)
                return;

            string path = file.Path.LocalPath;
            string csv = LootCsvExporter.ToCsv(_all, _names);
            await LootCsvExporter.WriteAsync(path, csv).ConfigureAwait(true);

            int rows = _all.Sum(x => x.Items.Count);
            StatusMessage = string.Create(CultureInfo.InvariantCulture, $"已导出 {rows} 条明细到 {path}。");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"导出失败：{exception.Message}";
        }
    }

    private void RefreshTrend() =>
        Trend = LootTrend.Compute(_all, IsMonthly ? TrendGranularity.Month : TrendGranularity.Day);

    private string NameOf(Guid characterId) => _names.GetValueOrDefault(characterId, "未知角色");

    private static string Gil(int value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0} 吉尔");

    private static string Gil(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0} 吉尔");
}
