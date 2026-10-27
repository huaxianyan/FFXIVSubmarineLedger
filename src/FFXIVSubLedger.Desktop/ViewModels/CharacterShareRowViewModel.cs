using System.Globalization;
using FFXIVSubLedger.Application.Records;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>统计页里的一行角色占比。数值格式化在这里做，界面只绑字符串。</summary>
public sealed class CharacterShareRowViewModel(LootStatisticsCharacter item, string name)
{
    public string CharacterName { get; } = name;

    public double SharePercent { get; } = item.SharePercent;

    public string RecordCountLabel { get; } = string.Create(CultureInfo.InvariantCulture, $"{item.RecordCount} 次");

    public string ValueLabel { get; } = string.Create(CultureInfo.InvariantCulture, $"{item.TotalValue:N0} 吉尔");
}
