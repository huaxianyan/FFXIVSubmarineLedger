using CommunityToolkit.Mvvm.ComponentModel;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 尚未实现的页面占位。用于说明该页将要做什么，避免空窗口。
/// </summary>
/// <param name="description">一段话说明这个页面解决什么问题。</param>
/// <param name="steps">计划中的操作步骤。</param>
/// <param name="footnote">底部补充说明。</param>
public sealed class RoadmapPageViewModel(
    string description,
    IReadOnlyList<RoadmapStepViewModel> steps,
    string footnote) : ObservableObject
{
    public string Description { get; } = description;

    public IReadOnlyList<RoadmapStepViewModel> Steps { get; } = steps;

    public string Footnote { get; } = footnote;

    public static RoadmapPageViewModel CreateSettings() => new(
        "数据存放位置、识别参数与默认归属，都放在这里。",
        [
            new RoadmapStepViewModel(1, "选择默认角色，收艇时省一步选择"),
            new RoadmapStepViewModel(2, "查看并打开数据目录，方便备份数据库"),
            new RoadmapStepViewModel(3, "调整识别灵敏度与数量识别的最小格子尺寸"),
        ],
        "数据库是一个 SQLite 文件，整个目录复制走就等于完成备份。");
}

/// <summary>占位页里的一个步骤。</summary>
/// <param name="order">序号，从 1 开始。</param>
/// <param name="text">步骤说明。</param>
public sealed class RoadmapStepViewModel(int order, string text) : ObservableObject
{
    public string Order { get; } = order.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string Text { get; } = text;
}
