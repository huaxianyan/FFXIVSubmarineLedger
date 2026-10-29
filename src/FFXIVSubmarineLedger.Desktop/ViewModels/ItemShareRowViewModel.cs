using System.Globalization;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 统计页里的一行道具占比。数值格式化在这里做，界面只绑字符串——
/// XAML 的 StringFormat 一旦写歪不会编译失败，只会在运行时静默不生效。
/// </summary>
public sealed class ItemShareRowViewModel(LootStatisticsItem item, string displayName)
{
    public string ItemName { get; } = displayName;

    public double SharePercent { get; } = item.SharePercent;

    public string QuantityLabel { get; } = string.Create(CultureInfo.InvariantCulture, $"{item.Quantity:N0}");

    public string ValueLabel { get; } = GilFormat.Full(item.TotalValue);

    /// <summary>紧凑写法，让量级一眼看得出（3.63 亿 Gil）。</summary>
    public string CompactValueLabel { get; } = GilFormat.Compact(item.TotalValue);

    public bool HasCompactValue => item.TotalValue >= 10_000;
}
