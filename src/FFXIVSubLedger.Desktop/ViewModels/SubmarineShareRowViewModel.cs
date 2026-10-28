using System.Globalization;
using FFXIVSubLedger.Application.Records;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>按艇占比的一行。数值格式化在这里做，界面只绑字符串。</summary>
public sealed class SubmarineShareRowViewModel(LootStatisticsSubmarine item, string name)
{
    public string SubmarineName { get; } = name;

    public double SharePercent { get; } = item.SharePercent;

    public string RecordCountLabel { get; } = string.Create(CultureInfo.InvariantCulture, $"{item.RecordCount} 次");

    public string ValueLabel { get; } = string.Create(CultureInfo.InvariantCulture, $"{item.TotalValue:N0} 吉尔");
}

/// <summary>确认界面里「这一行属于哪艘艇」的选项。</summary>
/// <param name="Name">艇名；空串表示未标明。</param>
/// <param name="Label">下拉里显示的文字。</param>
public sealed record SubmarineChoice(string Name, string Label);

/// <summary>筛选下拉里的一项。</summary>
/// <param name="Id">对应的实体 ID；null 表示「全部」。</param>
/// <param name="Label">下拉里显示的文字。</param>
public sealed record FilterOption(Guid? Id, string Label);
