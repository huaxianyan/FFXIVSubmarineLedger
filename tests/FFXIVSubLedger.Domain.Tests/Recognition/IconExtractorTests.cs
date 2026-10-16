using FFXIVSubLedger.Infrastructure.Recognition;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

/// <summary>图标切分与归一化的测试。判据都是「同一道具必须匹配上、不同道具必须分得开」。</summary>
public sealed class IconExtractorTests
{
    private static readonly GridCell Cell = new(0, 0, 0, 0, 69, 69, false, 0, 0);

    [Fact]
    public void Extract_NormalizesToRequestedSize()
    {
        IconPatch patch = IconExtractor.Extract(Pattern(69, 69, 0), Cell, new IconExtractionOptions { PatchSize = 32 });

        Assert.Equal(32, patch.Size);
        Assert.Equal(32 * 32 * 3, patch.Pixels.Length);
        Assert.Equal(32 * 32, patch.Mask.Length);
    }

    [Fact]
    public void Extract_MasksOnlyTheQuantityRegion()
    {
        IconPatch patch = IconExtractor.Extract(Pattern(69, 69, 0), Cell);

        // 左上角是图标内容，右下角是数量数字。
        Assert.False(patch.Mask[0]);
        Assert.True(patch.Mask[(31 * 32) + 31]);

        // 数字只占右下角一小块，大部分像素仍要参与比对。
        Assert.InRange(patch.EffectiveRatio, 0.5, 0.85);
    }

    [Fact]
    public void Similarity_IgnoresMaskedQuantityDigits()
    {
        // 两张图只有右下角（数字区）不同，其余完全一样，相似度必须是满分。
        // 否则「27」和「111」会被判成两个不同的道具。
        RgbImage withDigits = Pattern(69, 69, 0);
        PaintRect(withDigits, 40, 48, 69, 69, 255, 255, 255);

        double score = IconExtractor.Extract(Pattern(69, 69, 0), Cell)
            .SimilarityTo(IconExtractor.Extract(withDigits, Cell));

        Assert.Equal(1.0, score, 3);
    }

    [Fact]
    public void Similarity_SurvivesBrightnessShift()
    {
        // 换滤镜、调亮度之后同一个道具还得认得出。
        RgbImage darker = Pattern(69, 69, -20);
        RgbImage brighter = Pattern(69, 69, 20);

        double score = IconExtractor.Extract(darker, Cell).SimilarityTo(IconExtractor.Extract(brighter, Cell));

        Assert.True(score > 0.85, $"亮度变化后的相似度只有 {score:F3}。");
    }

    [Fact]
    public void Similarity_SeparatesDifferentIcons()
    {
        // 不同颜色的两个道具必须分得开。
        // 这里刻意用最坏情况：两张图图案完全一样，只有颜色不同——
        // 结构项会给出满分，全靠颜色项把它们拉开。
        RgbImage red = Pattern(69, 69, 0, hue: 0);
        RgbImage cyan = Pattern(69, 69, 0, hue: Math.PI);

        double score = IconExtractor.Extract(red, Cell).SimilarityTo(IconExtractor.Extract(cyan, Cell));

        Assert.True(score < 0.45, $"两个不同道具的相似度高达 {score:F3}，区分度不足。");
    }

    [Fact]
    public void Similarity_SurvivesUiScaleChange()
    {
        // 同一个图标，一张是 69 px 格子，一张是 34 px 格子（不同 UI 倍率）。
        // 归一化到同一边长后应当仍然匹配。
        double score = IconExtractor.Extract(Pattern(69, 69, 0), new GridCell(0, 0, 0, 0, 69, 69, false, 0, 0))
            .SimilarityTo(IconExtractor.Extract(Pattern(34, 34, 0), new GridCell(0, 0, 0, 0, 34, 34, false, 0, 0)));

        Assert.True(score > 0.95, $"缩放后的相似度只有 {score:F3}。");
    }

    /// <summary>
    /// 造一张带径向图案的图标。hue 控制基础色相，shift 模拟整体亮度变化。
    /// 三个通道的基准值按色相错开 120°，这样换 hue 是真的换颜色，
    /// 而不是只动一个通道——只动一个通道的话，颜色差异会被其余通道的共有分量稀释掉。
    /// </summary>
    private static RgbImage Pattern(int width, int height, int shift, double hue = 0)
    {
        const double ThirdTurn = 2.0943951; // 120°

        double baseR = 128 + (Math.Cos(hue) * 70);
        double baseG = 128 + (Math.Cos(hue - ThirdTurn) * 70);
        double baseB = 128 + (Math.Cos(hue + ThirdTurn) * 70);

        var pixels = new byte[width * height * 3];
        double centreX = width / 2.0;
        double centreY = height / 2.0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = (x - centreX) / centreX;
                double dy = (y - centreY) / centreY;
                double radial = Math.Cos(Math.Sqrt((dx * dx) + (dy * dy)) * 3.0) * 40;

                int offset = (((y * width) + x) * 3);
                pixels[offset] = Clamp(baseR + radial + shift);
                pixels[offset + 1] = Clamp(baseG + radial + shift);
                pixels[offset + 2] = Clamp(baseB + radial + shift);
            }
        }

        return new RgbImage(width, height, pixels);
    }

    private static void PaintRect(RgbImage image, int left, int top, int right, int bottom, byte r, byte g, byte b)
    {
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                int offset = (((y * image.Width) + x) * 3);
                image.Pixels[offset] = r;
                image.Pixels[offset + 1] = g;
                image.Pixels[offset + 2] = b;
            }
        }
    }

    private static byte Clamp(double value) => (byte)Math.Clamp(value, 0, 255);
}
