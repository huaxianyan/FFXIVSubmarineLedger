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

    /// <summary>主显示：带单位的概数，一眼看出量级（99.5 万 Gil）。</summary>
    public string ValueLabel { get; } = GilFormat.Compact(item.TotalValue);

    /// <summary>副行：精确值，要核对时看（995,392 Gil）。</summary>
    public string DetailValueLabel { get; } = GilFormat.Full(item.TotalValue);

    public bool HasDetailValue => GilFormat.HasDetail(item.TotalValue);
}
