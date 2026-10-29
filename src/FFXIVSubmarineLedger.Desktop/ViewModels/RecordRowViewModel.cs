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

    /// <summary>主显示：带单位的概数（19 万 Gil）。</summary>
    public string TotalLabel { get; } = GilFormat.Compact(model.TotalValue);

    /// <summary>副行：精确值（190,177 Gil）。</summary>
    public string DetailTotalLabel { get; } = GilFormat.Full(model.TotalValue);

    public bool HasDetailTotal => GilFormat.HasDetail(model.TotalValue);

    public string DetailLabel { get; } = string.Join(
        "、",
        model.Items.Select(x => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{resolveItemName(x.ItemId, x.ItemName)}{(x.IsHighQuality ? "(HQ)" : string.Empty)}×{x.Quantity}")));

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}
