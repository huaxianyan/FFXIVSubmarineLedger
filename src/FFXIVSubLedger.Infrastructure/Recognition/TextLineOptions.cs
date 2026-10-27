namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 日志文本行定位的阈值。默认值是按真实日服日志截图（756 × 798）定的，换分辨率不用改，
/// 换字体大小也不用改——行高是从图里量出来的，只有绝对阈值需要按游戏亮度调。
/// </summary>
public sealed class TextLineOptions
{
    /// <summary>算作「文字像素」的亮度下限。日志字是白色带黑描边，200 能把字和大部分场景分开。</summary>
    public byte BrightnessThreshold { get; init; } = 200;

    /// <summary>一行里至少要有这么多文字像素才算文本行，用来滤掉零星高光。</summary>
    public int MinPixelsPerRow { get; init; } = 5;

    /// <summary>行带的绝对最小高度。低于它的直接当噪声，实测日志区上下边缘会混进 2–4 px 的亮边。</summary>
    public int MinBandHeight { get; init; } = 6;

    /// <summary>相对行高中位数的下限比例。行高明显偏小的带子不可能是文字行。</summary>
    public double HeightFloorRatio { get; init; } = 0.55;

    /// <summary>相对行高中位数的上限比例。明显偏大的带子通常是几行粘在一起或背景亮块，宁可丢掉。</summary>
    public double HeightCeilingRatio { get; init; } = 1.8;
}
