using FFXIVSubmarineLedger.Infrastructure.Recognition;

namespace FFXIVSubmarineLedger.Domain.Tests.Recognition;

public sealed class LinePreprocessorTests
{
    private const byte Background = 20;
    private const byte Ink = 255;

    [Fact]
    public void Process_ScalesTheLineUpForOcr()
    {
        GrayImage image = InkOnDark(width: 40, height: 20);
        var line = new TextLine(0, Top: 5, Height: 10, Left: 10, Right: 29);

        TextLineImage processed = LinePreprocessor.Process(image, line, scale: 3);

        // 行带左右各留 2 px 余量：(29 - 10 + 1 + 4) × 3。
        Assert.Equal(72, processed.Width);
        Assert.Equal((10 + 4) * 3, processed.Height);
    }

    [Fact]
    public void Process_LeavesBlackGlyphsOnWhiteBackground()
    {
        GrayImage image = InkOnDark(width: 40, height: 20);
        var line = new TextLine(0, Top: 5, Height: 10, Left: 10, Right: 29);

        TextLineImage processed = LinePreprocessor.Process(image, line, scale: 1);

        // 第一行是行带外的余量，应该是背景色（反相后为白）。
        Assert.Equal(255, processed.Pixels[0]);

        // 行带中的文字像素应反相成黑。
        int inkOffset = (6 * processed.Width) + 12;
        Assert.Equal(0, processed.Pixels[inkOffset]);
    }

    private static GrayImage InkOnDark(int width, int height)
    {
        var pixels = new byte[width * height];
        Array.Fill(pixels, Background);

        for (int y = 5; y < 15; y++)
        {
            for (int x = 10; x < 30; x++)
                pixels[(y * width) + x] = Ink;
        }

        return new GrayImage(width, height, pixels);
    }
}
