using FFXIVSubmarineLedger.Infrastructure.Recognition;

namespace FFXIVSubmarineLedger.Domain.Tests.Recognition;

public sealed class TextLineLocatorTests
{
    private const byte Background = 20;
    private const byte Ink = 255;

    [Fact]
    public void Locate_FindsEachTextBandInOrder()
    {
        GrayImage image = Canvas(width: 400, height: 220, ["9,25", "39,25", "69,24", "99,25", "129,25"]);

        IReadOnlyList<TextLine> lines = TextLineLocator.Locate(image);

        Assert.Equal(5, lines.Count);
        Assert.Equal([9, 39, 69, 99, 129], lines.Select(x => x.Top));
        Assert.Equal([0, 1, 2, 3, 4], lines.Select(x => x.Index));
    }

    [Fact]
    public void Locate_ReportsTheInkExtentOfEachLine()
    {
        GrayImage image = Canvas(width: 400, height: 80, ["10,20"], left: 13, inkWidth: 200);

        TextLine line = Assert.Single(TextLineLocator.Locate(image));

        Assert.Equal(13, line.Left);
        Assert.Equal(212, line.Right);
        Assert.Equal(20, line.Height);
    }

    [Fact]
    public void Locate_IgnoresBandsFarShorterThanALine()
    {
        // 真实样本上日志区上下边缘会混进 2–4 px 的亮边，不能当成一行。
        GrayImage image = Canvas(width: 400, height: 220, ["0,4", "9,25", "39,25", "69,25", "210,3"]);

        IReadOnlyList<TextLine> lines = TextLineLocator.Locate(image);

        Assert.Equal(3, lines.Count);
        Assert.Equal([9, 39, 69], lines.Select(x => x.Top));
    }

    [Fact]
    public void Locate_ReturnsNothingWhenTheImageHasNoText()
    {
        GrayImage image = Canvas(width: 200, height: 100, []);

        Assert.Empty(TextLineLocator.Locate(image));
    }

    [Fact]
    public void Locate_SplitsBandsGluedTogetherByABrightBackground()
    {
        // 2026-11-22 的真实截图：日志区下半压在亮色地板上，一片亮背景把三行粘成一条 89 px 的带子，
        // 整块被行高上限丢掉，最后一行收益直接不见了。切开之后三行都得回来。
        GrayImage image = BrightBackgroundCanvas();

        IReadOnlyList<TextLine> lines = TextLineLocator.Locate(image);

        Assert.Equal(5, lines.Count);
        Assert.Equal(5, lines[0].Top);
        Assert.Equal(165, lines[^1].Top);

        // 亮背景里那三行：切出来的边界可能上下多带一两行（笔画描边的余量），不该差得更远。
        foreach ((int expected, int index) in new[] { (50, 1), (90, 2), (130, 3) })
            Assert.InRange(lines[index].Top, expected - 2, expected + 2);

        Assert.All(lines, line => Assert.InRange(line.Height, 11, 15));
    }

    /// <summary>
    /// 上下各有正常暗底行，中间一块成片的亮背景（没有描边，亮度却过阈值），里面三行「字」。
    /// 「字」画成斜条纹：每一行都有足够多亮像素（亮度投影能连成带），每个亮像素上下两像素内
    /// 又一定有深色（描边投影认得出来）。日志字是白字带黑描边，正是这个形状；
    /// 而成片的亮背景两个条件都过不了。
    /// </summary>
    private static GrayImage BrightBackgroundCanvas()
    {
        const int width = 400;
        const int height = 200;
        var pixels = new byte[width * height];
        Array.Fill(pixels, Background);

        for (int y = 40; y < 150; y++)
        {
            for (int x = 0; x < width; x++)
                pixels[(y * width) + x] = 210;
        }

        foreach (int top in new[] { 5, 50, 90, 130, 165 })
        {
            for (int y = top; y < top + 11; y++)
            {
                for (int x = 13; x < 313; x++)
                    pixels[(y * width) + x] = (x + y) % 5 < 3 ? Ink : Background;
            }
        }

        return new GrayImage(width, height, pixels);
    }

    /// <summary>
    /// 造一张暗底白字的图。<paramref name="bands"/> 的每项是「上边缘,高度」，文字块横向从
    /// <paramref name="left"/> 起、宽 <paramref name="inkWidth"/>，够宽就能压过逐行的像素阈值。
    /// </summary>
    private static GrayImage Canvas(
        int width,
        int height,
        string[] bands,
        int left = 13,
        int inkWidth = 300)
    {
        var pixels = new byte[width * height];
        Array.Fill(pixels, Background);

        foreach (string band in bands)
        {
            string[] parts = band.Split(',');
            int top = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            int tall = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

            for (int y = top; y < top + tall && y < height; y++)
            {
                for (int x = left; x < left + inkWidth && x < width; x++)
                    pixels[(y * width) + x] = Ink;
            }
        }

        return new GrayImage(width, height, pixels);
    }
}
