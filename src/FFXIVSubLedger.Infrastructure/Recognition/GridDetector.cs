namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 从背包截图里推算格子网格。
///
/// 做法沿用预研脚本（<c>tools/analyze_grid.py</c>）验证过的路线：
/// 边缘能量投影 → 自相关求主周期 → 相位搜索对齐网格线。
///
/// 关键是**正方形先验**：格子是正方形，而行方向的可见行数往往很少（收艇截图常见只有 2 行），
/// 行向自相关不可靠（多尺度实测只有 3/7 档命中，列向 7/7 命中）。
/// 所以只用列方向定周期，行方向沿用同一个周期、只单独搜相位。
/// </summary>
public static class GridDetector
{
    public static GridDetectionResult? Detect(GrayImage image, GridDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= new GridDetectionOptions();

        double[] columnEdges = ColumnEdges(image);
        double[] rowEdges = RowEdges(image);

        int minLag = Math.Max(3, options.MinCellSize);
        int columnMaxLag = Math.Max(minLag + 1, (int)(image.Width * options.MaxCellSizeRatio));

        PeriodResult? columnPeriod = DominantPeriod(columnEdges, minLag, columnMaxLag);
        if (columnPeriod is not { } column || column.Correlation < options.MinColumnCorrelation)
            return null;

        int cellSize = column.Period;
        int offsetX = BestPhase(columnEdges, cellSize).Phase;
        PhaseResult rowPhase = BestPhase(rowEdges, cellSize);

        // 行向单独检出的周期只作为诊断信息带出去，不参与对齐。
        int rowMaxLag = Math.Max(minLag + 1, (int)(image.Height * options.MaxCellSizeRatio));
        PeriodResult? rowPeriod = DominantPeriod(rowEdges, minLag, rowMaxLag);

        IReadOnlyList<int> verticalLines = GridLines(image.Width, cellSize, offsetX);
        IReadOnlyList<int> horizontalLines = GridLines(image.Height, cellSize, rowPhase.Phase);

        if (verticalLines.Count < 2 || horizontalLines.Count < 2)
            return null;

        return new GridDetectionResult(
            cellSize,
            verticalLines,
            horizontalLines,
            column.Correlation,
            rowPeriod?.Correlation ?? 0,
            rowPhase.Sharpness,
            BuildCells(image, verticalLines, horizontalLines, options));
    }

    /// <summary>列方向的边缘能量：每一列与左邻列的灰度差之和。格子边界会在曲线上形成等距峰。</summary>
    private static double[] ColumnEdges(GrayImage image)
    {
        byte[] pixels = image.Pixels;
        int width = image.Width;
        int height = image.Height;
        var edges = new double[width];

        for (int x = 1; x < width; x++)
        {
            long sum = 0;
            for (int y = 0; y < height; y++)
            {
                int index = (y * width) + x;
                sum += Math.Abs(pixels[index] - pixels[index - 1]);
            }

            edges[x] = sum;
        }

        return edges;
    }

    /// <summary>行方向的边缘能量，含义同上。</summary>
    private static double[] RowEdges(GrayImage image)
    {
        byte[] pixels = image.Pixels;
        int width = image.Width;
        int height = image.Height;
        var edges = new double[height];

        for (int y = 1; y < height; y++)
        {
            int rowStart = y * width;
            int previousRowStart = rowStart - width;
            long sum = 0;

            for (int x = 0; x < width; x++)
                sum += Math.Abs(pixels[rowStart + x] - pixels[previousRowStart + x]);

            edges[y] = sum;
        }

        return edges;
    }

    /// <summary>
    /// 用自相关找主周期。先在相关曲线上找局部极大，取最高者；
    /// 找不到峰（曲线单调）时退回全局最大。
    /// </summary>
    private static PeriodResult? DominantPeriod(double[] signal, int minLag, int maxLag)
    {
        maxLag = Math.Min(maxLag, signal.Length - 1);
        if (maxLag <= minLag)
            return null;

        double mean = 0;
        foreach (double value in signal)
            mean += value;
        mean /= signal.Length;

        var centered = new double[signal.Length];
        double denominator = 0;
        for (int i = 0; i < signal.Length; i++)
        {
            centered[i] = signal[i] - mean;
            denominator += centered[i] * centered[i];
        }

        if (denominator <= 0)
            return null;

        var correlations = new double[maxLag + 1];
        for (int lag = minLag; lag <= maxLag; lag++)
            correlations[lag] = Correlate(centered, denominator, lag);

        PeriodResult? best = null;
        for (int lag = minLag + 1; lag < maxLag; lag++)
        {
            double current = correlations[lag];
            if (current <= 0)
                continue;

            bool isPeak = current >= correlations[lag - 1] && current >= correlations[lag + 1];
            if (isPeak && (best is null || current > best.Value.Correlation))
                best = new PeriodResult(lag, current);
        }

        if (best is not null)
            return best;

        for (int lag = minLag; lag <= maxLag; lag++)
        {
            if (best is null || correlations[lag] > best.Value.Correlation)
                best = new PeriodResult(lag, correlations[lag]);
        }

        return best;
    }

    private static double Correlate(double[] centered, double denominator, int lag)
    {
        double sum = 0;
        int limit = centered.Length - lag;
        for (int i = 0; i < limit; i++)
            sum += centered[i] * centered[i + lag];

        return sum / denominator;
    }

    /// <summary>在 [0, period) 里搜一个相位，让落在网格线上的边缘能量之和最大。</summary>
    private static PhaseResult BestPhase(double[] signal, int period)
    {
        int bestPhase = 0;
        double bestScore = double.NegativeInfinity;
        double totalScore = 0;

        for (int phase = 0; phase < period; phase++)
        {
            double score = 0;
            for (int position = phase; position < signal.Length; position += period)
                score += signal[position];

            totalScore += score;

            if (score > bestScore)
            {
                bestScore = score;
                bestPhase = phase;
            }
        }

        double averageScore = totalScore / period;
        double sharpness = averageScore > 0 ? bestScore / averageScore : 0;
        return new PhaseResult(bestPhase, bestScore, sharpness);
    }

    /// <summary>
    /// 从相位出发按周期铺线，右侧允许最多超出 10% 个格子，超出的那条吸附到图像边缘。
    ///
    /// 为什么留这个容差：截图常常是贴着最后一格的右边缘裁的，此时那条真正的边界正好落在
    /// <paramref name="length"/> 上。用严格小于会把它丢掉，整整少算一列——实测样本就踩了这个坑
    /// （5 列被算成 4 列，最右边一列的道具整个丢失）。容差又不能给大，否则右侧留白会被当成一列。
    /// 同理，周期或相位估偏 1 像素时也靠这点容差兜住。
    /// </summary>
    private static IReadOnlyList<int> GridLines(int length, int period, int phase)
    {
        int tolerance = Math.Max(2, period / 10);
        var lines = new List<int>();

        for (int position = phase; position <= length + tolerance; position += period)
        {
            int line = Math.Min(position, length);

            // 吸附之后可能和上一条重合（相位本身就在边缘上），别造出宽度为 0 的格子。
            if (lines.Count > 0 && line == lines[^1])
                break;

            lines.Add(line);
        }

        return lines;
    }

    private static IReadOnlyList<GridCell> BuildCells(
        GrayImage image,
        IReadOnlyList<int> verticalLines,
        IReadOnlyList<int> horizontalLines,
        GridDetectionOptions options)
    {
        var cells = new List<GridCell>((verticalLines.Count - 1) * (horizontalLines.Count - 1));

        for (int row = 0; row < horizontalLines.Count - 1; row++)
        {
            for (int column = 0; column < verticalLines.Count - 1; column++)
            {
                int x0 = verticalLines[column];
                int x1 = verticalLines[column + 1];
                int y0 = horizontalLines[row];
                int y1 = horizontalLines[row + 1];

                // 向内收缩一圈，把格子边框排除在统计之外：边框的明暗差会把空位误判成有道具。
                int inset = Math.Max(
                    0,
                    Math.Min(
                        (int)Math.Round((x1 - x0) * options.CellInsetRatio),
                        (int)Math.Round((y1 - y0) * options.CellInsetRatio)));

                int statsX0 = Math.Min(x0 + inset, x1 - 1);
                int statsY0 = Math.Min(y0 + inset, y1 - 1);

                (double mean, double standardDeviation) =
                    PatchStatistics(image, statsX0, statsY0, x1 - inset, y1 - inset);
                bool isEmpty = standardDeviation < options.EmptyCellStdDevThreshold;

                cells.Add(new GridCell(
                    column,
                    row,
                    x0,
                    y0,
                    x1 - x0,
                    y1 - y0,
                    isEmpty,
                    mean,
                    standardDeviation));
            }
        }

        return cells;
    }

    private static (double Mean, double StandardDeviation) PatchStatistics(
        GrayImage image,
        int x0,
        int y0,
        int x1,
        int y1)
    {
        long count = 0;
        double sum = 0;
        double sumOfSquares = 0;

        for (int y = y0; y < y1; y++)
        {
            int rowStart = y * image.Width;
            for (int x = x0; x < x1; x++)
            {
                double value = image.Pixels[rowStart + x];
                sum += value;
                sumOfSquares += value * value;
                count++;
            }
        }

        if (count == 0)
            return (0, 0);

        double mean = sum / count;
        double variance = Math.Max(0, (sumOfSquares / count) - (mean * mean));
        return (mean, Math.Sqrt(variance));
    }

    private readonly record struct PeriodResult(int Period, double Correlation);

    private readonly record struct PhaseResult(int Phase, double Score, double Sharpness);
}
