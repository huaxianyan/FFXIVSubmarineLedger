using System.Globalization;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>统计页里的一行角色占比。数值格式化在这里做，界面只绑字符串。</summary>
public sealed class CharacterShareRowViewModel(LootStatisticsCharacter item, string name)
{
    public string CharacterName { get; } = name;

    public double SharePercent { get; } = item.SharePercent;

    public string RecordCountLabel { get; } = Loc.Format("stats.times", item.RecordCount);

    public string ValueLabel { get; } = GilFormat.Full(item.TotalValue);

    /// <summary>紧凑写法，让量级一眼看得出（3.63 亿 Gil）。</summary>
    public string CompactValueLabel { get; } = GilFormat.Compact(item.TotalValue);

    public bool HasCompactValue => item.TotalValue >= 10_000;
}
