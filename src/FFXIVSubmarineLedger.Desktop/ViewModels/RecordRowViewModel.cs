using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>统计页里的一条已入账记录。</summary>
public sealed partial class RecordRowViewModel(
    LootRecord model,
    string characterLabel,
    StatisticsPageViewModel owner,
    Func<uint, string, string> resolveItemName) : ObservableObject
{
    internal Guid Id => model.Id;

    public string DateLabel { get; } = model.Date.ToString("yyyy-MM-dd");

    public string CharacterLabel { get; } = characterLabel;

    public string TotalLabel { get; } = GilFormat.Full(model.TotalValue);

    /// <summary>紧凑写法（99.5 万 Gil）。一万以下不显示。</summary>
    public string CompactTotalLabel { get; } = GilFormat.Compact(model.TotalValue);

    public bool HasCompactTotal => model.TotalValue >= 10_000;

    public string DetailLabel { get; } = string.Join(
        "、",
        model.Items.Select(x => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{resolveItemName(x.ItemId, x.ItemName)}{(x.IsHighQuality ? "(HQ)" : string.Empty)}×{x.Quantity}")));

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}
