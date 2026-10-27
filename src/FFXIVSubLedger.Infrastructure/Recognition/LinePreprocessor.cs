namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 把一条文本行加工成 OCR 能吃的图：裁行带 → 放大 → 二值化 → 反相成白底黑字。
///
/// 二值化阈值按行内亮度用 Otsu 求，不写死固定值——日志窗口没有底板，同一屏里
/// 深色场景与亮色场景挨在一起，固定阈值必然在其中一侧失效。
/// </summary>
public static class LinePreprocessor
{
    /// <summary>放大倍数。实测 25 px 高的日文字直接喂 OCR 识别率明显偏低，放大后稳得多。</summary>
    public const int DefaultScale = 3;

    /// <summary>行带外再留一点余量，避免裁掉假名的小勾与浊点。</summary>
    private const int Margin = 2;

    public static TextLineImage Process(GrayImage image, TextLine line, int scale = DefaultScale)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);

        int left = Math.Max(0, line.Left - Margin);
        int right = Math.Min(image.Width - 1, line.Right + Margin);
        int top = Math.Max(0, line.Top - Margin);
        int bottom = Math.Min(image.Height - 1, line.Bottom + Margin);

        int sourceWidth = right - left + 1;
        int sourceHeight = bottom - top + 1;

        int width = sourceWidth * scale;
        int height = sourceHeight * scale;

        byte threshold = Otsu(image, left, top, sourceWidth, sourceHeight);
        var pixels = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            double sourceY = top + ((y + 0.5) / scale) - 0.5;

            for (int x = 0; x < width; x++)
            {
                double sourceX = left + ((x + 0.5) / scale) - 0.5;
                byte value = SampleBilinear(image, sourceX, sourceY);

                // 反相：文字本来是亮的，OCR 要白底黑字。
                // 用严格大于：Otsu 返回的是背景类的最高灰度，把它自己算成文字会把整行变黑。
                pixels[(y * width) + x] = value > threshold ? (byte)0 : (byte)255;
            }
        }

        return new TextLineImage(width, height, pixels);
    }

    private static byte SampleBilinear(GrayImage image, double x, double y)
    {
        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = Math.Min(image.Width - 1, x0 + 1);
        int y1 = Math.Min(image.Height - 1, y0 + 1);

        x0 = Math.Clamp(x0, 0, image.Width - 1);
        y0 = Math.Clamp(y0, 0, image.Height - 1);

        double fx = x - x0;
        double fy = y - y0;

        double top = (image[x0, y0] * (1 - fx)) + (image[x1, y0] * fx);
        double bottom = (image[x0, y1] * (1 - fx)) + (image[x1, y1] * fx);

        return (byte)Math.Clamp((top * (1 - fy)) + (bottom * fy), 0, 255);
    }

    private static byte Otsu(GrayImage image, int left, int top, int width, int height)
    {
        Span<int> histogram = stackalloc int[256];
        for (int y = top; y < top + height; y++)
        {
            for (int x = left; x < left + width; x++)
                histogram[image[x, y]]++;
        }

        int total = width * height;
        double sum = 0;
        for (int i = 0; i < 256; i++)
            sum += i * histogram[i];

        double sumBackground = 0;
        int weightBackground = 0;
        double bestVariance = -1;
        byte bestThreshold = 128;

        for (int t = 0; t < 256; t++)
        {
            weightBackground += histogram[t];
            if (weightBackground == 0)
                continue;

            int weightForeground = total - weightBackground;
            if (weightForeground == 0)
                break;

            sumBackground += t * histogram[t];

            double meanBackground = sumBackground / weightBackground;
            double meanForeground = (sum - sumBackground) / weightForeground;
            double delta = meanBackground - meanForeground;
            double variance = (double)weightBackground * weightForeground * delta * delta;

            if (variance > bestVariance)
            {
                bestVariance = variance;
                bestThreshold = (byte)t;
            }
        }

        return bestThreshold;
    }
}
