namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>网格检测的可调参数。</summary>
public sealed record GridDetectionOptions
{
    /// <summary>格子边长的搜索下限（像素）。低于这个尺寸的数量数字已经读不清，没有识别价值。</summary>
    public int MinCellSize { get; init; } = 24;

    /// <summary>格子边长的搜索上限，按图像宽（或高）的比例给出。</summary>
    public double MaxCellSizeRatio { get; init; } = 0.5;

    /// <summary>列向自相关峰值低于这个值就判定为「没有网格」，避免对非背包截图瞎猜。</summary>
    public double MinColumnCorrelation { get; init; } = 0.2;

    /// <summary>
    /// 统计格位灰度时向内收缩的比例，用来把格子的边框排除在外。
    /// 边框的明暗差会把空位误判成有道具，而且它本来也不属于图标主体。
    /// </summary>
    public double CellInsetRatio { get; init; } = 0.12;

    /// <summary>格位灰度标准差低于这个值判为空位（纯背景）。样本实测空格子约 10，有图标的 44–71。</summary>
    public double EmptyCellStdDevThreshold { get; init; } = 15;
}
