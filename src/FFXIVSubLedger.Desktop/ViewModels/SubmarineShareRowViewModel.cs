using FFXIVSubLedger.Domain.Settings;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Application.Records;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>按艇占比的一行。数值格式化在这里做，界面只绑字符串。</summary>
public sealed class SubmarineShareRowViewModel(LootStatisticsSubmarine item, string name)
{
    public string SubmarineName { get; } = name;

    public double SharePercent { get; } = item.SharePercent;

    public string RecordCountLabel { get; } = Loc.Format("stats.times", item.RecordCount);

    public string ValueLabel { get; } = string.Create(CultureInfo.InvariantCulture, $"{item.TotalValue:N0} Gil");
}

/// <summary>
/// 确认界面里「这一行属于哪艘艇」的选项。换语言时「未标明艇」那项要跟着变，
/// 所以是可观察对象，同一份实例在多行之间共享。
/// </summary>
public sealed partial class SubmarineChoice(string name) : ObservableObject
{
    /// <summary>艇名；空串表示未标明。</summary>
    public string Name { get; } = name;

    /// <summary>下拉里显示的文字。艇名本身不翻译，只有「未标明」那一项跟着语言走。</summary>
    public string Label => Name.Length > 0 ? Name : Loc.Get("records.subUnmarked");

    public void Relocalize() => OnPropertyChanged(nameof(Label));
}

/// <summary>显示语言下拉里的一项。语言名用各自的语言书写，不随界面语言变。</summary>
/// <param name="Tag">语言标签，如 <c>zh-Hans</c>。</param>
/// <param name="Label">该语言自己的写法，如 <c>日本語</c>。</param>
public sealed record LanguageChoice(string Tag, string Label);

/// <summary>代理模式下拉里的一项。</summary>
public sealed record ProxyOption(ProxyMode Mode, string Label);

/// <summary>筛选下拉里的一项。</summary>
/// <param name="Id">对应的实体 ID；null 表示「全部」。</param>
/// <param name="Label">下拉里显示的文字。</param>
public sealed record FilterOption(Guid? Id, string Label);
