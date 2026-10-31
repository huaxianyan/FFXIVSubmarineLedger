using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Records;
using FFXIVSubmarineLedger.Infrastructure.Catalog;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 收艇记录页：逐条明细，是整本账的原始数据。
///
/// 角色/艇的筛选也放在这里——收益统计页只做汇总，用的是<b>同一份筛选结果</b>
/// （<see cref="FilteredRecords" />，变化时抛 <see cref="FilterChanged" />），
/// 这样两页的口径永远一致，不会出现「明细 5 笔、统计算了 7 笔」。
/// </summary>
public sealed partial class LedgerPageViewModel(
    ILootRecordRepository records,
    ISubmarineRepository submarines,
    CharacterService characters,
    Lazy<ItemCatalog> catalog) : ObservableObject
{
    private IReadOnlyList<LootRecord> _all = [];
    private IReadOnlyDictionary<Guid, string> _names = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, string> _submarineNames = new Dictionary<Guid, string>();
    private IReadOnlyList<LootRecord> _filtered = [];

    public ObservableCollection<RecordRowViewModel> Records { get; } = [];

    /// <summary>角色筛选下拉。第一项固定是「全部角色」。</summary>
    public ObservableCollection<FilterOption> CharacterOptions { get; } = [];

    /// <summary>艇筛选下拉。必须先选角色才会填内容。</summary>
    public ObservableCollection<FilterOption> SubmarineOptions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFilterBySubmarine))]
    private FilterOption? _selectedCharacter;

    [ObservableProperty]
    private FilterOption? _selectedSubmarine;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecords))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoaded;

    /// <summary>艇名在角色范围内才唯一，所以没选角色时不给筛艇。</summary>
    public bool CanFilterBySubmarine => SelectedCharacter?.Id is not null;

    /// <summary>「共 12 笔」。筛完还剩多少，一眼能看到。</summary>
    public string CountLabel => Loc.Format("ledger.count", Records.Count);

    public bool HasRecords => Records.Count > 0;

    public bool IsEmpty => IsLoaded && Records.Count == 0;

    /// <summary>当前筛选后的记录，供收益统计页算汇总。</summary>
    public IReadOnlyList<LootRecord> FilteredRecords => _filtered;

    /// <summary>角色 ID → 显示名（含服务器），导出 CSV 要用。</summary>
    public IReadOnlyDictionary<Guid, string> CharacterNames => _names;

    /// <summary>筛选结果变了。统计页据此重算。</summary>
    public event EventHandler? FilterChanged;

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

    /// <summary>换语言后重来一遍：筛选下拉的标签和物品名都要换成当前语言。</summary>
    public void Relocalize() => _ = LoadAsync();

    /// <summary>按当前的筛选条件重算这一页。</summary>
    private void ApplyFilter()
    {
        IReadOnlyList<LootRecord> filtered = _all;

        if (SelectedCharacter?.Id is { } characterId)
            filtered = [.. filtered.Where(x => x.CharacterId == characterId)];

        if (SelectedSubmarine?.Id is { } submarineId)
            filtered = [.. filtered.Where(x => x.SubmarineId == submarineId)];

        _filtered = filtered;

        // 新的在前：刚记的那笔一眼能看到。
        Records.Clear();
        foreach (LootRecord record in filtered.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt))
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
        FilterChanged?.Invoke(this, EventArgs.Empty);
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
            .OrderBy(id => SubmarineNameOf(id), StringComparer.Ordinal);

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
            return catalog.Value.Find(itemId);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or FileNotFoundException)
        {
            return null;
        }
    }
}
