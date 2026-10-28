using CommunityToolkit.Mvvm.ComponentModel;
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

    public LootRowViewModel(RecognizedLoot loot)
    {
        LineNumber = loot.LineNumber;
        ItemText = loot.ItemText;
        SubmarineName = loot.SubmarineName;
        _candidates = loot.Candidates;
        SelectedItem = loot.Item;
        _quantity = loot.Quantity;
        _isHighQuality = loot.IsHighQuality;
        _unitPriceOverride = 0;
        // 词表外的物名也是真收益，默认勾上，只差一个单价；否则整批都未知时界面看着像「什么都没读到」。
        _isIncluded = true;
    }

    public int LineNumber { get; }

    /// <summary>这批收益属于哪艘艇。日志里没等到「探索完了」行时为 null。</summary>
    public string? SubmarineName { get; }

    /// <summary>OCR 读出的原始物名。没命中词表时靠它判断。</summary>
    public string ItemText { get; }

    /// <summary>可选的道具。只有歧义时才有多个。</summary>
    public IReadOnlyList<LootCatalogEntry> Candidates => _candidates;

    public bool HasCandidateChoice => _candidates.Count > 1;

    public string SubmarineLabel => SubmarineName is { Length: > 0 } name ? name : "未标明艇";

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

    public string ItemName => SelectedItem?.Name ?? ItemText;

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
        ? "词表里没有，填单价后才入账"
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
