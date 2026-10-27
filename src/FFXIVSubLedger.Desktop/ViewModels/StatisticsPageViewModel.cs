using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Characters;
using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 收益统计页。口径全在 <see cref="LootStatistics"/> 里，这一层只负责把数摆到界面上。
/// </summary>
public sealed partial class StatisticsPageViewModel(
    ILootRecordRepository records,
    CharacterService characters) : ObservableObject
{
    public ObservableCollection<ItemShareRowViewModel> Items { get; } = [];

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
    [NotifyPropertyChangedFor(nameof(HasRecords))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    /// <summary>每日收益走势。</summary>
    [ObservableProperty]
    private TrendSeries? _trend;

    public bool HasRecords => Records.Count > 0;

    public bool IsEmpty => IsLoaded && Records.Count == 0;

    public async Task LoadAsync()
    {
        IReadOnlyList<LootRecord> all = await records.ListAsync().ConfigureAwait(true);
        IReadOnlyList<Character> owners = await characters.ListAsync(includeInactive: true).ConfigureAwait(true);

        Dictionary<Guid, string> names = owners.ToDictionary(x => x.Id, x => x.DisplayName);

        LootStatisticsSnapshot stats = LootStatistics.Compute(all);

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

        Trend = LootTrend.Compute(all, TrendGranularity.Day);

        Records.Clear();
        foreach (LootRecord record in all)
        {
            string label = names.TryGetValue(record.CharacterId, out string? name) ? name : "未知角色";
            Records.Add(new RecordRowViewModel(record, label, this));
        }

        IsLoaded = true;
        OnPropertyChanged(nameof(HasRecords));
    }

    internal async Task DeleteAsync(RecordRowViewModel row)
    {
        await records.DeleteAsync(row.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    private static string Gil(int value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0} 吉尔");

    private static string Gil(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0} 吉尔");
}
