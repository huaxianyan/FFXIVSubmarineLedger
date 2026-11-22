namespace FFXIVSubmarineLedger.Infrastructure.Recognition;

/// <summary>
/// 从日志截图里切出文本行带。
///
/// 做法是行方向投影 + 行高归一：先按行统计亮像素得到候选带，再用行高中位数把
/// 「明显不是一行的带子」滤掉。**不做左边界聚类过滤**——多留一行让人删，比
/// 少留一行让人重截便宜（旧图标路线吃过「宁可少算」的亏）。
///
/// 整段日志压在亮色场景上时（实战里很常见），一片亮背景会把相邻几行粘成一条过高的带子。
/// 这种带子不再直接丢，而是用「带黑描边的亮像素」在带内重找行——日志字是白字带黑边，
/// 笔画细、边上总有深色，成片的亮背景周围没有深色，两者分得开。
/// </summary>
public static class TextLineLocator
{
    public static IReadOnlyList<TextLine> Locate(GrayImage image, TextLineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= new TextLineOptions();

        List<(int Top, int Height)> bands = FindBands(image, options);

        // 1–2 px 的碎带不参与中位数：它们只是笔画上下边缘的残留，混进来会把「典型行高」
        // 拉得很低，相对上限跟着变低，真正的文字行反而被判成「太高」而整批丢掉。
        List<(int Top, int Height)> candidates = [.. bands.Where(band => band.Height >= options.MinBandHeight)];
        if (candidates.Count == 0)
            return [];

        int median = Median([.. candidates.Select(x => x.Height)]);
        int minHeight = Math.Max(options.MinBandHeight, (int)(median * options.HeightFloorRatio));
        int maxHeight = Math.Max(minHeight, (int)Math.Ceiling(median * options.HeightCeilingRatio));

        var lines = new List<TextLine>(candidates.Count);
        foreach ((int top, int height) in candidates)
        {
            foreach ((int lineTop, int lineHeight, bool tighten) in Resolve(image, top, height, options, minHeight, maxHeight))
            {
                (int left, int right) = HorizontalExtent(image, lineTop, lineHeight, options, tighten);
                lines.Add(new TextLine(lines.Count, lineTop, lineHeight, left, right));
            }
        }

        return lines;
    }

    /// <summary>
    /// 把一条候选带变成零到多条文本行：高度正常就原样收下，过高就切，过矮就丢。
    /// 切出来的行多一个 <c>tighten</c> 标记——它们周围本来就是成片的亮背景，
    /// 左右边界得按描边像素收紧，否则亮背景块会一起被裁进去，二值化阈值被拉偏。
    /// </summary>
    private static List<(int Top, int Height, bool Tighten)> Resolve(
        GrayImage image,
        int top,
        int height,
        TextLineOptions options,
        int minHeight,
        int maxHeight)
    {
        if (height < minHeight)
            return [];

        if (height <= maxHeight)
            return [(top, height, false)];

        return [.. FindOutlinedBands(image, top, height, options)
            .Where(band => band.Height >= minHeight && band.Height <= maxHeight)
            .Select(band => (band.Top, band.Height, true))];
    }

    private static List<(int Top, int Height)> FindBands(GrayImage image, TextLineOptions options) =>
        ScanBands(image, 0, image.Height, options, CountBrightPixelsInRow);

    /// <summary>在一条过高带子的行范围内，只按「带黑描边的亮像素」重找行。</summary>
    private static List<(int Top, int Height)> FindOutlinedBands(
        GrayImage image,
        int top,
        int height,
        TextLineOptions options) =>
        ScanBands(image, top, top + height, options, CountOutlinedPixelsInRow);

    /// <summary>逐行数文字像素，把连续够数的行连成带。</summary>
    private static List<(int Top, int Height)> ScanBands(
        GrayImage image,
        int from,
        int to,
        TextLineOptions options,
        Func<GrayImage, int, TextLineOptions, int> countRow)
    {
        var bands = new List<(int Top, int Height)>();

        int runTop = -1;
        for (int y = from; y < to; y++)
        {
            bool isText = countRow(image, y, options) >= options.MinPixelsPerRow;

            if (isText && runTop < 0)
            {
                runTop = y;
            }
            else if (!isText && runTop >= 0)
            {
                bands.Add((runTop, y - runTop));
                runTop = -1;
            }
        }

        if (runTop >= 0)
            bands.Add((runTop, to - runTop));

        return bands;
    }

    private static int CountBrightPixelsInRow(GrayImage image, int y, TextLineOptions options)
    {
        int count = 0;
        int offset = y * image.Width;

        for (int x = 0; x < image.Width; x++)
        {
            if (image.Pixels[offset + x] >= options.BrightnessThreshold)
                count++;
        }

        return count;
    }

    private static int CountOutlinedPixelsInRow(GrayImage image, int y, TextLineOptions options)
    {
        int count = 0;
        int offset = y * image.Width;

        for (int x = 0; x < image.Width; x++)
        {
            if (image.Pixels[offset + x] >= options.BrightnessThreshold && HasDarkNeighbour(image, x, y, options))
                count++;
        }

        return count;
    }

    /// <summary>上下 <see cref="TextLineOptions.OutlineRadius"/> 像素内有没有深色描边。</summary>
    private static bool HasDarkNeighbour(GrayImage image, int x, int y, TextLineOptions options)
    {
        for (int dy = -options.OutlineRadius; dy <= options.OutlineRadius; dy++)
        {
            if (dy == 0)
                continue;

            int neighbour = y + dy;
            if (neighbour < 0 || neighbour >= image.Height)
                continue;

            if (image.Pixels[(neighbour * image.Width) + x] <= options.OutlineThreshold)
                return true;
        }

        return false;
    }

    private static (int Left, int Right) HorizontalExtent(
        GrayImage image,
        int top,
        int height,
        TextLineOptions options,
        bool requireOutline)
    {
        int left = image.Width;
        int right = -1;

        for (int y = top; y < top + height; y++)
        {
            int offset = y * image.Width;
            for (int x = 0; x < image.Width; x++)
            {
                if (image.Pixels[offset + x] < options.BrightnessThreshold)
                    continue;

                if (requireOutline && !HasDarkNeighbour(image, x, y, options))
                    continue;

                if (x < left)
                    left = x;

                if (x > right)
                    right = x;
            }
        }

        // 候选带保证至少有一个亮像素，这里兜一下防御性返回，避免空行给出 -1 坐标。
        return right < 0 ? (0, 0) : (left, right);
    }

    private static int Median(int[] values)
    {
        int[] sorted = [.. values.Order()];
        int middle = sorted.Length / 2;

        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
