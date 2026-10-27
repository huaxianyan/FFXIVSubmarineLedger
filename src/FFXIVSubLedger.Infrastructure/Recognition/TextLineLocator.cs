namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 从日志截图里切出文本行带。
///
/// 做法是行方向投影 + 行高归一：先按行统计亮像素得到候选带，再用行高中位数把
/// 「明显不是一行的带子」滤掉。**不做左边界聚类过滤**——多留一行让人删，比
/// 少留一行让人重截便宜（旧图标路线吃过「宁可少算」的亏）。
/// </summary>
public static class TextLineLocator
{
    public static IReadOnlyList<TextLine> Locate(GrayImage image, TextLineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        options ??= new TextLineOptions();

        List<(int Top, int Height)> bands = FindBands(image, options);
        if (bands.Count == 0)
            return [];

        int median = Median([.. bands.Select(x => x.Height)]);
        int minHeight = Math.Max(options.MinBandHeight, (int)(median * options.HeightFloorRatio));
        int maxHeight = Math.Max(minHeight, (int)Math.Ceiling(median * options.HeightCeilingRatio));

        var lines = new List<TextLine>(bands.Count);
        foreach ((int top, int height) in bands)
        {
            if (height < minHeight || height > maxHeight)
                continue;

            (int left, int right) = HorizontalExtent(image, top, height, options);
            lines.Add(new TextLine(lines.Count, top, height, left, right));
        }

        return lines;
    }

    private static List<(int Top, int Height)> FindBands(GrayImage image, TextLineOptions options)
    {
        var bands = new List<(int Top, int Height)>();

        int runTop = -1;
        for (int y = 0; y < image.Height; y++)
        {
            bool isText = CountBrightPixelsInRow(image, y, options) >= options.MinPixelsPerRow;

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
            bands.Add((runTop, image.Height - runTop));

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

    private static (int Left, int Right) HorizontalExtent(GrayImage image, int top, int height, TextLineOptions options)
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
