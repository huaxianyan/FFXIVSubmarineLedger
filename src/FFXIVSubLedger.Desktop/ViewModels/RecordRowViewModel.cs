using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Desktop.ViewModels;

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

    public string TotalLabel { get; } = string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"{model.TotalValue:N0}");

    public string DetailLabel { get; } = string.Join(
        "、",
        model.Items.Select(x => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{resolveItemName(x.ItemId, x.ItemName)}{(x.IsHighQuality ? "(HQ)" : string.Empty)}×{x.Quantity}")));

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}
