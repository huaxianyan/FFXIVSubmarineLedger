using FFXIVSubLedger.Infrastructure.Recognition;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

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
