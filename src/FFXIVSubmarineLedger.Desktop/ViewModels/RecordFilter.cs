using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 角色 / 艇的筛选条件。收艇记录页和收益统计页各持一份，界面上的样式一致
/// （标题栏里那一块，见 <c>MainWindow</c>）。
/// </summary>
public sealed partial class RecordFilter : ObservableObject
{
    private IReadOnlyList<LootRecord> _all = [];
    private Func<Guid, string> _submarineNameOf = _ => string.Empty;

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

    /// <summary>筛选变了。持有者据此重算自己的数据。</summary>
    public event EventHandler? Changed;

    partial void OnSelectedCharacterChanged(FilterOption? value) => RebuildSubmarines();

    partial void OnSelectedSubmarineChanged(FilterOption? value) =>
        Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 按当前的角色与记录重建两个下拉，然后通知一次。
    /// 艇的可选项取自这个角色实际有记录的艇：没有记录的艇拿来筛也没意义。
    /// </summary>
    public void Reset(
        IReadOnlyList<Character> owners,
        IReadOnlyList<LootRecord> all,
        Func<Guid, string> submarineNameOf)
    {
        _all = all;
        _submarineNameOf = submarineNameOf;

        CharacterOptions.Clear();
        CharacterOptions.Add(new FilterOption(null, Loc.Get("stats.allCharacters")));
        foreach (Character owner in owners)
            CharacterOptions.Add(new FilterOption(owner.Id, owner.DisplayName));

        // 保持用户当前的选择；数据源变了也不该把选择悄悄丢掉。
        FilterOption? previous = SelectedCharacter;
        SelectedCharacter = previous?.Id is { } id && CharacterOptions.Any(x => x.Id == id)
            ? CharacterOptions.First(x => x.Id == id)
            : CharacterOptions[0];

        RebuildSubmarines();
    }

    /// <summary>把筛选条件套到记录上。</summary>
    public IReadOnlyList<LootRecord> Apply(IReadOnlyList<LootRecord> all)
    {
        IEnumerable<LootRecord> filtered = all;

        if (SelectedCharacter?.Id is { } characterId)
            filtered = filtered.Where(x => x.CharacterId == characterId);

        if (SelectedSubmarine?.Id is { } submarineId)
            filtered = filtered.Where(x => x.SubmarineId == submarineId);

        return [.. filtered];
    }

    private void RebuildSubmarines()
    {
        SubmarineOptions.Clear();
        SubmarineOptions.Add(new FilterOption(null, Loc.Get("stats.allSubmarines")));

        if (SelectedCharacter?.Id is { } characterId)
        {
            IEnumerable<Guid> submarineIds = _all
                .Where(x => x.CharacterId == characterId && x.SubmarineId is not null)
                .Select(x => x.SubmarineId!.Value)
                .Distinct()
                .OrderBy(_submarineNameOf, StringComparer.Ordinal);

            foreach (Guid submarineId in submarineIds)
                SubmarineOptions.Add(new FilterOption(submarineId, _submarineNameOf(submarineId)));
        }

        // 换了角色，原来选的艇不属于这个人了，回到「全部艇」。
        FilterOption? previous = SelectedSubmarine;
        SelectedSubmarine = previous?.Id is { } id && SubmarineOptions.Any(x => x.Id == id)
            ? SubmarineOptions.First(x => x.Id == id)
            : SubmarineOptions[0];

        // 上面的赋值可能已经触发过 Changed（艇确实换了），这里再通知一次保证「换角色」生效。
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
