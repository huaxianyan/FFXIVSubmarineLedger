using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Domain.Records;
using FFXIVSubmarineLedger.Infrastructure.Catalog;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 收益统计页。口径全在 <see cref="LootStatistics"/> 与 <see cref="LootTrend"/> 里，
/// 这一层只负责把数摆到界面上。
///
/// 明细和筛选都在 <see cref="LedgerPageViewModel" />：两页共用同一份筛选结果，
/// 不会出现「明细 5 笔、统计算了 7 笔」。
/// </summary>
public sealed partial class StatisticsPageViewModel(
    LedgerPageViewModel ledger,
    Func<TopLevel?> topLevelProvider,
    Lazy<ItemCatalog> catalog) : ObservableObject
{
    private IReadOnlyList<LootRecord> _filtered = [];

    public ObservableCollection<ItemShareRowViewModel> Items { get; } = [];

    public ObservableCollection<CharacterShareRowViewModel> Characters { get; } = [];

    public ObservableCollection<SubmarineShareRowViewModel> SubmarineShares { get; } = [];

    [ObservableProperty]
    private string _totalLabel = "0";

    /// <summary>总收益的精确值（363,215,608 Gil），作副行。一万以下为空，不占位。</summary>
    [ObservableProperty]
    private string _totalDetailLabel = string.Empty;

    [ObservableProperty]
    private string _averagePerDayDetailLabel = string.Empty;

    [ObservableProperty]
    private string _averagePerMonthDetailLabel = string.Empty;

    [ObservableProperty]
    private string _averagePerYearDetailLabel = string.Empty;

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

    public string TrendTitle => IsMonthly ? Loc.Get("stats.trendMonth") : Loc.Get("stats.trendDay");

    public bool HasStatus => StatusMessage.Length > 0;

    public bool HasRecords => _filtered.Count > 0;

    public bool IsEmpty => IsLoaded && _filtered.Count == 0;

    /// <summary>每日收益走势。</summary>
    [ObservableProperty]
    private TrendSeries? _trend;

    /// <summary>换语言后重算页内文字。</summary>
    public void Relocalize()
    {
        OnPropertyChanged(nameof(TrendTitle));
        _ = LoadAsync();
    }

    /// <summary>明细页筛好了，这里跟着重算一次汇总。</summary>
    public void Load()
    {
        Apply(ledger.FilteredRecords);
        IsLoaded = true;
    }

    /// <summary>刷新数据源后重算。</summary>
    public async Task LoadAsync()
    {
        await ledger.LoadAsync().ConfigureAwait(true);
        Load();
    }

    private void Apply(IReadOnlyList<LootRecord> filtered)
    {
        _filtered = filtered;
        LootStatisticsSnapshot stats = LootStatistics.Compute(filtered);

        TotalLabel = GilCompact(stats.TotalValue);
        TotalDetailLabel = GilDetail(stats.TotalValue);
        AveragePerDayLabel = GilCompact(stats.AveragePerDay);
        AveragePerDayDetailLabel = GilDetail(stats.AveragePerDay);
        AveragePerMonthLabel = GilCompact(stats.AveragePerMonth);
        AveragePerMonthDetailLabel = GilDetail(stats.AveragePerMonth);
        AveragePerYearLabel = GilCompact(stats.AveragePerYear);
        AveragePerYearDetailLabel = GilDetail(stats.AveragePerYear);

        SpanLabel = stats.RecordCount == 0
            ? Loc.Get("stats.noRecords")
            : Loc.Format("stats.span", $"{stats.FirstDate:yyyy-MM-dd}", stats.OperatingDays, stats.RecordCount);

        ItemCountLabel = Loc.Format("stats.itemCount", stats.ItemCount);

        Items.Clear();
        foreach (LootStatisticsItem item in stats.ItemsByValue)
            Items.Add(new ItemShareRowViewModel(item, ResolveItemName(item.ItemId, item.ItemName)));

        Characters.Clear();
        foreach (LootStatisticsCharacter item in stats.ByCharacter)
            Characters.Add(new CharacterShareRowViewModel(item, ledger.NameOf(item.CharacterId)));

        SubmarineShares.Clear();
        foreach (LootStatisticsSubmarine item in stats.BySubmarine)
            SubmarineShares.Add(new SubmarineShareRowViewModel(item, ledger.SubmarineNameOf(item.SubmarineId)));

        RefreshTrend();

        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(IsEmpty));
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
            StatusMessage = Loc.Get("stats.pickerUnavailable");
            return;
        }

        try
        {
            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Loc.Get("stats.exportTitle"),
                SuggestedFileName = string.Create(
                    CultureInfo.InvariantCulture,
                    $"subledger-{DateTime.Now:yyyyMMdd-HHmm}.csv"),
                FileTypeChoices =
                [
                    new FilePickerFileType(Loc.Get("stats.csvType"))
                    {
                        Patterns = ["*.csv"],
                        MimeTypes = ["text/csv"],
                    },
                ],
            }).ConfigureAwait(true);

            if (file is null)
                return;

            string path = file.Path.LocalPath;
            string csv = LootCsvExporter.ToCsv(_filtered, ledger.CharacterNames);
            await LootCsvExporter.WriteAsync(path, csv).ConfigureAwait(true);

            int rows = _filtered.Sum(x => x.Items.Count);
            StatusMessage = Loc.Format("stats.exported", rows, path);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("stats.exportFailed", exception.Message);
        }
    }

    private void RefreshTrend() =>
        Trend = LootTrend.Compute(_filtered, IsMonthly ? TrendGranularity.Month : TrendGranularity.Day);

    /// <summary>落库记的是日志语言的名字；显示时按 ItemId 换成界面语言。</summary>
    private string ResolveItemName(uint itemId, string fallback) =>
        itemId != 0 && TryFindItem(itemId) is { } entry ? entry.DisplayName : fallback;

    private LootCatalogEntry? TryFindItem(uint itemId)
    {
        try
        {
            return catalog.Value.Find(itemId);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>主显示：带单位的概数。</summary>
    private static string GilCompact(long value) => GilFormat.Compact(value);

    private static string GilCompact(double value) => GilFormat.Compact((long)value);

    /// <summary>副行：精确值。一万以下与主显示相同，留空不占位。</summary>
    private static string GilDetail(long value) => GilFormat.HasDetail(value) ? GilFormat.Full(value) : string.Empty;

    private static string GilDetail(double value) => GilDetail((long)value);
}
