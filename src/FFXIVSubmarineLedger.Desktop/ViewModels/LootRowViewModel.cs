using CommunityToolkit.Mvvm.ComponentModel;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 确认界面里的一行：一条认出（或人工补上）的入手记录，数量和品质可改。
///
/// 识别给的是建议值，不是结论。HQ 标记被 OCR 吞掉、歧义物名被猜错都要在这一层改掉。
/// </summary>
public sealed partial class LootRowViewModel : ObservableObject
{
    private readonly IReadOnlyList<LootCatalogEntry> _candidates;

    /// <param name="loot">识别结果。</param>
    /// <param name="characterChoices">可归属的角色。</param>
    /// <param name="selectedCharacter">日志里读到的角色，认不出时为 <c>null</c>。</param>
    /// <param name="submarineChoices">该角色名下的艇 + 「未标明」，供人工纠正归属。</param>
    /// <param name="characterNote">角色名需要人看一眼时的提示（读歪了、认不出），其余情况为空。</param>
    /// <param name="submarineNote">艇名需要人看一眼时的提示（读歪了、分不清），其余情况为空。</param>
    public LootRowViewModel(
        RecognizedLoot loot,
        IReadOnlyList<CharacterRef> characterChoices,
        CharacterRef? selectedCharacter,
        IReadOnlyList<SubmarineChoice> submarineChoices,
        string characterNote = "",
        string submarineNote = "")
    {
        CharacterChoices = characterChoices;
        CharacterNote = characterNote;
        SubmarineNote = submarineNote;
        RawCharacterName = loot.CharacterName ?? string.Empty;
        RawSubmarineName = loot.SubmarineName ?? string.Empty;
        _selectedCharacter = selectedCharacter;
        _submarineChoices = submarineChoices;
        _selectedSubmarine = loot.SubmarineName is { Length: > 0 } name
            ? submarineChoices.FirstOrDefault(x => x.Name == name) ?? new SubmarineChoice(name)
            : submarineChoices[0];

        LineNumber = loot.LineNumber;
        ItemText = loot.ItemText;
        _candidates = loot.Candidates;
        SelectedItem = loot.Item;
        _quantity = loot.Quantity;
        _isHighQuality = loot.IsHighQuality;
        _unitPriceOverride = 0;
        // 词表外的物名也是真收益，默认勾上，只差一个单价；否则整批都未知时界面看着像「什么都没读到」。
        _isIncluded = true;
    }

    public int LineNumber { get; }

    /// <summary>这一行可以归到哪个角色。日志里读到的角色名作为默认值。</summary>
    public IReadOnlyList<CharacterRef> CharacterChoices { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterLabel))]
    [NotifyPropertyChangedFor(nameof(CharacterId))]
    private CharacterRef? _selectedCharacter;

    public Guid? CharacterId => SelectedCharacter?.Id;

    public string CharacterLabel => SelectedCharacter?.Label ?? Loc.Get("records.characterUnmarked");

    /// <summary>日志里读到的角色名原文，换角色时用来重新对艇名。</summary>
    public string RawCharacterName { get; }

    /// <summary>日志里读到的艇名原文，换角色时用来重新对艇名。</summary>
    public string RawSubmarineName { get; }

    /// <summary>角色名需要人看一眼时的提示，比如「角色名读成「Tilna Branford」，已按「Tiina Branford」记」。</summary>
    public string CharacterNote { get; private set; }

    public bool HasCharacterNote => CharacterNote.Length > 0;

    public void SetCharacterNote(string note)
    {
        CharacterNote = note;
        OnPropertyChanged(nameof(CharacterNote));
        OnPropertyChanged(nameof(HasCharacterNote));
    }

    /// <summary>这一行属于哪艘艇的可选项。换角色时会整批换掉。</summary>
    [ObservableProperty]
    private IReadOnlyList<SubmarineChoice> _submarineChoices;

    /// <summary>
    /// 换了角色就换一套艇选项：艇名只在角色范围内唯一。
    /// 原来的选择还在就保留，不在就回到「未标明」。
    /// </summary>
    /// <param name="choices">新角色名下的艇 + 「未标明」。</param>
    /// <param name="preferred">想选中的艇名（重新对过之后的结果），空或不在列表里就回到「未标明」。</param>
    public void ResetSubmarines(IReadOnlyList<SubmarineChoice> choices, string? preferred)
    {
        SubmarineChoices = choices;
        SelectedSubmarine = choices.FirstOrDefault(x => x.Name == preferred) ?? choices[0];
    }

    /// <summary>
    /// 归属的艇。识别可能把相邻两艘艇的收益串在一起（收益行文本完全相同时尤其容易），
    /// 所以这里必须能人工改。空串表示未标明。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubmarineName))]
    [NotifyPropertyChangedFor(nameof(SubmarineLabel))]
    private SubmarineChoice _selectedSubmarine;

    public string? SubmarineName => string.IsNullOrEmpty(SelectedSubmarine.Name) ? null : SelectedSubmarine.Name;

    /// <summary>OCR 读出的原始物名。没命中词表时靠它判断。</summary>
    public string ItemText { get; }

    /// <summary>可选的道具。只有歧义时才有多个。</summary>
    public IReadOnlyList<LootCatalogEntry> Candidates => _candidates;

    public bool HasCandidateChoice => _candidates.Count > 1;

    public string SubmarineLabel => SubmarineName ?? Loc.Get("records.subUnmarked");

    /// <summary>
    /// 艇名需要人看一眼时的提示，比如「艇名读成「Strawberrv」，已按「Strawberry」记」。
    /// 空串表示这一行的归属没歧义。
    /// </summary>
    public string SubmarineNote { get; private set; }

    public bool HasSubmarineNote => SubmarineNote.Length > 0;

    public void SetSubmarineNote(string note)
    {
        SubmarineNote = note;
        OnPropertyChanged(nameof(SubmarineNote));
        OnPropertyChanged(nameof(HasSubmarineNote));
    }

    /// <summary>换语言后刷新这一行里由文案拼出来的部分。</summary>
    public void Relocalize()
    {
        foreach (SubmarineChoice choice in SubmarineChoices)
            choice.Relocalize();

        OnPropertyChanged(nameof(SubmarineLabel));
        OnPropertyChanged(nameof(CharacterLabel));
        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(PriceLabel));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemName))]
    [NotifyPropertyChangedFor(nameof(IsUnknown))]
    [NotifyPropertyChangedFor(nameof(CanBeHighQuality))]
    [NotifyPropertyChangedFor(nameof(UnitPrice))]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private LootCatalogEntry? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitPrice))]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private decimal _quantity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitPrice))]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private bool _isHighQuality;

    /// <summary>词表外的物名要手填单价，填了才能入账。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnitPrice))]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private decimal _unitPriceOverride;

    /// <summary>取消勾选就不入账，用于删掉误收的行。</summary>
    [ObservableProperty]
    private bool _isIncluded;

    public string ItemName => SelectedItem?.DisplayName ?? ItemText;

    public bool IsUnknown => SelectedItem is null;

    /// <summary>物名能不能是 HQ。不能的话把开关置灰，避免用户勾了却没效果。</summary>
    public bool CanBeHighQuality => SelectedItem?.CanBeHq == true;

    /// <summary>物名允许 HQ 且用户勾了 HQ，才算 HQ 价。</summary>
    public bool EffectiveHighQuality => IsHighQuality && SelectedItem?.CanBeHq == true;

    public long UnitPrice => SelectedItem is { } item
        ? ShopPrice.For(item.ShopPriceNq, EffectiveHighQuality)
        : (long)Math.Max(0, UnitPriceOverride);

    public int EffectedQuantity => (int)Math.Max(0, Quantity);

    public long Subtotal => UnitPrice * EffectedQuantity;

    public string PriceLabel => IsUnknown
        ? Loc.Get("row.unknownPriceHint")
        : string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"@ {UnitPrice:N0}{(EffectiveHighQuality ? "（HQ）" : string.Empty)} = {Subtotal:N0}");

    /// <summary>
    /// 落库时一律用规范名（词表里的第一门写法），不用当前界面语言的名字：
    /// 存成显示名的话，切换语言后再落库的记录就会和之前的名字对不上。
    /// </summary>
    public LootEntryInput ToEntry() => new(
        SelectedItem?.ItemId ?? 0,
        SelectedItem?.CanonicalName ?? ItemText,
        EffectedQuantity,
        EffectiveHighQuality,
        UnitPrice,
        IsUnknown);

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(PriceLabel));

    partial void OnIsHighQualityChanged(bool value) => OnPropertyChanged(nameof(PriceLabel));

    partial void OnSelectedItemChanged(LootCatalogEntry? value) => OnPropertyChanged(nameof(PriceLabel));

    partial void OnUnitPriceOverrideChanged(decimal value) => OnPropertyChanged(nameof(PriceLabel));
}
