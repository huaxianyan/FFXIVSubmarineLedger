using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Records;
using FFXIVSubmarineLedger.Infrastructure.Catalog;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 收艇记录页：逐条明细。筛选在标题栏里（<c>MainWindow</c>），
/// 与收益统计页同一套样式，但各持一份条件、互不影响。
/// </summary>
public sealed partial class LedgerPageViewModel : ObservableObject
{
    private readonly ILootRecordRepository _records;
    private readonly ISubmarineRepository _submarines;
    private readonly CharacterService _characters;
    private readonly Func<TopLevel?> _topLevelProvider;
    private readonly Lazy<ItemCatalog> _catalog;

    private IReadOnlyList<LootRecord> _all = [];
    private IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, string> _submarineNames = new Dictionary<Guid, string>();

    public LedgerPageViewModel(
        ILootRecordRepository records,
        ISubmarineRepository submarines,
        CharacterService characters,
        Func<TopLevel?> topLevelProvider,
        Lazy<ItemCatalog> catalog)
    {
        _records = records;
        _submarines = submarines;
        _characters = characters;
        _topLevelProvider = topLevelProvider;
        _catalog = catalog;

        Filter.Changed += (_, _) => ApplyFilter();
    }

    /// <summary>这一页自己的筛选条件。</summary>
    public RecordFilter Filter { get; } = new();

    public ObservableCollection<RecordRowViewModel> Records { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecords))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    public string CountLabel => Loc.Format("ledger.count", Records.Count);

    public bool HasRecords => Records.Count > 0;

    public bool IsEmpty => IsLoaded && Records.Count == 0;

    public async Task LoadAsync()
    {
        _all = await _records.ListAsync().ConfigureAwait(true);
        IReadOnlyList<Character> owners = await _characters.ListAsync(includeInactive: true).ConfigureAwait(true);
        IReadOnlyList<Submarine> allSubmarines = await _submarines.ListAsync().ConfigureAwait(true);

        _names = owners.ToDictionary(x => x.Id, x => x.DisplayName);
        _submarineNames = allSubmarines.ToDictionary(x => x.Id, x => x.Name);

        Filter.Reset(owners, _all, id => SubmarineNameOf(id));
        ApplyFilter();

        IsLoaded = true;
    }

    /// <summary>换语言后重来一遍：筛选下拉的标签和物品名都要换成当前语言。</summary>
    public void Relocalize() => _ = LoadAsync();

    private void ApplyFilter()
    {
        // 新的在前：刚记的那笔一眼能看到。
        IReadOnlyList<LootRecord> filtered = [.. Filter
            .Apply(_all)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAt)];

        Records.Clear();
        foreach (LootRecord record in filtered)
        {
            Records.Add(new RecordRowViewModel(
                record,
                NameOf(record.CharacterId),
                SubmarineNameOf(record.SubmarineId),
                this,
                ResolveItemName));
        }

        OnPropertyChanged(nameof(CountLabel));
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(IsEmpty));
    }

    internal async Task DeleteAsync(RecordRowViewModel row)
    {
        await _records.DeleteAsync(row.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }

    internal string NameOf(Guid characterId) =>
        _names.GetValueOrDefault(characterId, Loc.Get("stats.unknownCharacter"));

    internal string SubmarineNameOf(Guid? submarineId) => submarineId is { } id
        ? _submarineNames.GetValueOrDefault(id, Loc.Get("stats.unknownSubmarine"))
        : Loc.Get("records.subUnmarked");

    /// <summary>落库记的是日志语言的名字；显示时按 ItemId 换成界面语言。</summary>
    private string ResolveItemName(uint itemId, string fallback) =>
        itemId != 0 && TryFindItem(itemId) is { } entry ? entry.DisplayName : fallback;

    private LootCatalogEntry? TryFindItem(uint itemId)
    {
        try
        {
            return _catalog.Value.Find(itemId);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException)
        {
            return null;
        }
    }
}
