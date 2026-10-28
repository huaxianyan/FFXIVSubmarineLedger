using CommunityToolkit.Mvvm.ComponentModel;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Catalog;
using FFXIVSubLedger.Infrastructure.Parsing;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 确认界面里的一行：一条认出（或人工补上）的入手记录，数量和品质可改。
///
/// 识别给的是建议值，不是结论。HQ 标记被 OCR 吞掉、歧义物名被猜错都要在这一层改掉。
/// </summary>
public sealed partial class LootRowViewModel : ObservableObject
{
    private readonly IReadOnlyList<LootCatalogEntry> _candidates;

    /// <param name="loot">识别结果。</param>
    /// <param name="submarineChoices">本次读取里出现过的艇，供人工纠正归属。</param>
    public LootRowViewModel(RecognizedLoot loot, IReadOnlyList<SubmarineChoice> submarineChoices)
    {
        SubmarineChoices = submarineChoices;
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

    /// <summary>这一行属于哪艘艇的可选项。日志里的艇名会作为默认值。</summary>
    public IReadOnlyList<SubmarineChoice> SubmarineChoices { get; }

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

    /// <summary>换语言后刷新这一行里由文案拼出来的部分。</summary>
    public void Relocalize()
    {
        foreach (SubmarineChoice choice in SubmarineChoices)
            choice.Relocalize();

        OnPropertyChanged(nameof(SubmarineLabel));
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

    public int UnitPrice => SelectedItem is { } item
        ? ShopPrice.For(item.ShopPriceNq, EffectiveHighQuality)
        : (int)Math.Max(0, UnitPriceOverride);

    public int EffectedQuantity => (int)Math.Max(0, Quantity);

    public int Subtotal => UnitPrice * EffectedQuantity;

    public string PriceLabel => IsUnknown
        ? Loc.Get("row.unknownPriceHint")
        : string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"@ {UnitPrice:N0}{(EffectiveHighQuality ? "（HQ）" : string.Empty)} = {Subtotal:N0}");

    public LootEntryInput ToEntry() => new(
        SelectedItem?.ItemId ?? 0,
        ItemName,
        EffectedQuantity,
        EffectiveHighQuality,
        UnitPrice,
        IsUnknown);

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(PriceLabel));

    partial void OnIsHighQualityChanged(bool value) => OnPropertyChanged(nameof(PriceLabel));

    partial void OnSelectedItemChanged(LootCatalogEntry? value) => OnPropertyChanged(nameof(PriceLabel));

    partial void OnUnitPriceOverrideChanged(decimal value) => OnPropertyChanged(nameof(PriceLabel));
}
