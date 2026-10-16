namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>一个格位的切分结果。</summary>
/// <param name="Column">列号，从 0 开始，按切分顺序。</param>
/// <param name="Row">行号，从 0 开始。</param>
/// <param name="X">左上角 X。</param>
/// <param name="Y">左上角 Y。</param>
/// <param name="Width">宽度（像素）。</param>
/// <param name="Height">高度（像素）。</param>
/// <param name="IsEmpty">是否是空位。空位不参与图标匹配。</param>
/// <param name="Mean">格位灰度均值。</param>
/// <param name="StandardDeviation">格位灰度标准差。</param>
public sealed record GridCell(
    int Column,
    int Row,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsEmpty,
    double Mean,
    double StandardDeviation);

/// <summary>网格检测结果。只包含完整格子，被图像边缘切掉的残缺格位已经丢弃。</summary>
/// <param name="CellSize">检出的格子边长（像素），按「正方形先验」由列方向周期得出。</param>
/// <param name="VerticalLines">所有竖线的 X 坐标。</param>
/// <param name="HorizontalLines">所有横线的 Y 坐标。</param>
/// <param name="ColumnCorrelation">列向自相关峰值，越高说明周期性越明确。</param>
/// <param name="RowCorrelation">行向独立检出的自相关峰值，仅作参考——行数太少时它不可靠，实际不用它。</param>
/// <param name="PhaseSharpness">相位锐度：最佳相位得分 / 各相位平均得分。接近 1 说明网格线没有落在真正的边界上。</param>
/// <param name="Cells">切分出的格位，按行优先排列。</param>
public sealed record GridDetectionResult(
    int CellSize,
    IReadOnlyList<int> VerticalLines,
    IReadOnlyList<int> HorizontalLines,
    double ColumnCorrelation,
    double RowCorrelation,
    double PhaseSharpness,
    IReadOnlyList<GridCell> Cells)
{
    public int Columns => Math.Max(0, VerticalLines.Count - 1);

    public int Rows => Math.Max(0, HorizontalLines.Count - 1);

    public int OccupiedCount => Cells.Count(cell => !cell.IsEmpty);

    public int EmptyCount => Cells.Count(cell => cell.IsEmpty);

    /// <summary>
    /// 对齐是否可信。阈值取的是经验值，不可信时界面仍会把网格画出来，只是提示用户核对。
    /// </summary>
    public bool IsConfident => ColumnCorrelation >= 0.25 && PhaseSharpness >= 1.15;
}
