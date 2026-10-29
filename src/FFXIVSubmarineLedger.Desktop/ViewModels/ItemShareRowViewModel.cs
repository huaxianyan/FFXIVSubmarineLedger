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

    /// <summary>主显示：带单位的概数，一眼看出量级（99.5 万 Gil）。</summary>
    public string ValueLabel { get; } = GilFormat.Compact(item.TotalValue);

    /// <summary>副行：精确值，要核对时看（995,392 Gil）。</summary>
    public string DetailValueLabel { get; } = GilFormat.Full(item.TotalValue);

    public bool HasDetailValue => GilFormat.HasDetail(item.TotalValue);
}
