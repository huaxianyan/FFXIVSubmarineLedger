using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Catalog;
using FFXIVSubLedger.Domain.Characters;
using FFXIVSubLedger.Domain.Records;
using FFXIVSubLedger.Infrastructure.Catalog;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 收益统计页。口径全在 <see cref="LootStatistics"/> 与 <see cref="LootTrend"/> 里，
/// 这一层只负责把数摆到界面上。
/// </summary>
public sealed partial class StatisticsPageViewModel(
    ILootRecordRepository records,
    ISubmarineRepository submarines,
    CharacterService characters,
    Func<TopLevel?> topLevelProvider,
    Lazy<ItemCatalog> catalog) : ObservableObject
{
    private IReadOnlyList<LootRecord> _all = [];
    private IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, string> _submarineNames = new Dictionary<Guid, string>();
    private IReadOnlyList<LootRecord> _filtered = [];

    public ObservableCollection<ItemShareRowViewModel> Items { get; } = [];

    public ObservableCollection<CharacterShareRowViewModel> Characters { get; } = [];

    public ObservableCollection<SubmarineShareRowViewModel> SubmarineShares { get; } = [];

    /// <summary>角色筛选下拉。第一项固定是「全部角色」。</summary>
    public ObservableCollection<FilterOption> CharacterOptions { get; } = [];

    /// <summary>艇筛选下拉。必须先选角色才会填内容。</summary>
    public ObservableCollection<FilterOption> SubmarineOptions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFilterBySubmarine))]
    private FilterOption? _selectedCharacter;

    [ObservableProperty]
    private FilterOption? _selectedSubmarine;

    /// <summary>艇名在角色范围内才唯一，所以没选角色时不给筛艇。</summary>
    public bool CanFilterBySubmarine => SelectedCharacter?.Id is not null;

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

    public string TrendTitle => IsMonthly ? Loc.Get("stats.trendMonth") : Loc.Get("stats.trendDay");

    /// <summary>换语言后重算页内文字。筛选下拉与统计值在 <see cref="LoadAsync"/> 里重建。</summary>
    public void Relocalize() => OnPropertyChanged(nameof(TrendTitle));

    public bool HasStatus => StatusMessage.Length > 0;

    public bool HasRecords => Records.Count > 0;

    public bool IsEmpty => IsLoaded && Records.Count == 0;

    /// <summary>每日收益走势。</summary>
    [ObservableProperty]
    private TrendSeries? _trend;

    partial void OnSelectedCharacterChanged(FilterOption? value)
    {
        RebuildSubmarineOptions();
        ApplyFilter();
    }

    partial void OnSelectedSubmarineChanged(FilterOption? value) => ApplyFilter();

    public async Task LoadAsync()
    {
        _all = await records.ListAsync().ConfigureAwait(true);
        IReadOnlyList<Character> owners = await characters.ListAsync(includeInactive: true).ConfigureAwait(true);
        IReadOnlyList<Submarine> allSubmarines = await submarines.ListAsync().ConfigureAwait(true);

        _names = owners.ToDictionary(x => x.Id, x => x.DisplayName);
        _submarineNames = allSubmarines.ToDictionary(x => x.Id, x => x.Name);

        CharacterOptions.Clear();
        CharacterOptions.Add(new FilterOption(null, Loc.Get("stats.allCharacters")));
        foreach (Character owner in owners)
            CharacterOptions.Add(new FilterOption(owner.Id, owner.DisplayName));

        // 保持用户当前的选择；换成另一个人时艇筛选要重置。
        FilterOption? previous = SelectedCharacter;
        SelectedCharacter = previous?.Id is { } id && CharacterOptions.Any(x => x.Id == id)
            ? CharacterOptions.First(x => x.Id == id)
            : CharacterOptions[0];

        RebuildSubmarineOptions();
        ApplyFilter();

        IsLoaded = true;
    }

    /// <summary>按当前的筛选条件重算整页。</summary>
    private void ApplyFilter()
    {
        IReadOnlyList<LootRecord> filtered = _all;

        if (SelectedCharacter?.Id is { } characterId)
            filtered = [.. filtered.Where(x => x.CharacterId == characterId)];

        if (SelectedSubmarine?.Id is { } submarineId)
            filtered = [.. filtered.Where(x => x.SubmarineId == submarineId)];

        _filtered = filtered;
        LootStatisticsSnapshot stats = LootStatistics.Compute(filtered);

        TotalLabel = Gil(stats.TotalValue);
        AveragePerDayLabel = Gil(stats.AveragePerDay);
        AveragePerMonthLabel = Gil(stats.AveragePerMonth);
        AveragePerYearLabel = Gil(stats.AveragePerYear);

        SpanLabel = stats.RecordCount == 0
            ? Loc.Get("stats.noRecords")
            : Loc.Format("stats.span", $"{stats.FirstDate:yyyy-MM-dd}", stats.OperatingDays, stats.RecordCount);

        ItemCountLabel = Loc.Format("stats.itemCount", stats.ItemCount);

        Items.Clear();
        foreach (LootStatisticsItem item in stats.ItemsByValue)
            Items.Add(new ItemShareRowViewModel(item, ResolveItemName(item.ItemId, item.ItemName)));

        Characters.Clear();
        foreach (LootStatisticsCharacter item in stats.ByCharacter)
            Characters.Add(new CharacterShareRowViewModel(item, NameOf(item.CharacterId)));

        SubmarineShares.Clear();
        foreach (LootStatisticsSubmarine item in stats.BySubmarine)
            SubmarineShares.Add(new SubmarineShareRowViewModel(item, SubmarineNameOf(item.SubmarineId)));

        Records.Clear();
        foreach (LootRecord record in filtered)
            Records.Add(new RecordRowViewModel(record, NameOf(record.CharacterId), this, ResolveItemName));

        RefreshTrend();

        OnPropertyChanged(nameof(HasRecords));
    }

    private void RebuildSubmarineOptions()
    {
        SubmarineOptions.Clear();
        SubmarineOptions.Add(new FilterOption(null, Loc.Get("stats.allSubmarines")));

        if (SelectedCharacter?.Id is not { } characterId)
        {
            SelectedSubmarine = SubmarineOptions[0];
            return;
        }

        // 艇的可选项取自这个角色实际有记录的艇：没有记录的艇拿来筛也没意义。
        IEnumerable<Guid> submarineIds = _all
            .Where(x => x.CharacterId == characterId && x.SubmarineId is not null)
            .Select(x => x.SubmarineId!.Value)
            .Distinct()
            .OrderBy(id => SubmarineNameOf(id), StringComparer.Ordinal)
            .Select(id => id);

        foreach (Guid submarineId in submarineIds)
            SubmarineOptions.Add(new FilterOption(submarineId, SubmarineNameOf(submarineId)));

        FilterOption? previous = SelectedSubmarine;
        SelectedSubmarine = previous?.Id is { } id && SubmarineOptions.Any(x => x.Id == id)
            ? SubmarineOptions.First(x => x.Id == id)
            : SubmarineOptions[0];
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
            string csv = LootCsvExporter.ToCsv(_filtered, _names);
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

    private string NameOf(Guid characterId) => _names.GetValueOrDefault(characterId, Loc.Get("stats.unknownCharacter"));

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

    private string SubmarineNameOf(Guid? submarineId) => submarineId is { } id
        ? _submarineNames.GetValueOrDefault(id, Loc.Get("stats.unknownSubmarine"))
        : Loc.Get("records.subUnmarked");

    private static string Gil(int value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");

    private static string Gil(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0} Gil");
}
