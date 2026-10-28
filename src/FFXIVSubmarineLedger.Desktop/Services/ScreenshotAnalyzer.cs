using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FFXIVSubmarineLedger.Infrastructure.Recognition;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 把 Avalonia 位图和识别算法接起来：读像素 → 转灰度 → 定位文本行 → 生成叠加预览图。
///
/// 会创建 WriteableBitmap，所以必须在 UI 线程上调用。日志截图通常不到一千像素见方，
/// 整个流程在毫秒级，直接同步跑不会卡界面。
/// </summary>
internal static class ScreenshotAnalyzer
{
    /// <summary>定位结果与叠加预览图。</summary>
    /// <param name="Lines">检出的文本行，自上而下。</param>
    /// <param name="Overlay">画了行框的预览图。</param>
    /// <param name="Gray">转换后的灰度图，供后续 OCR 使用。</param>
    /// <param name="Width">原图像素宽。</param>
    /// <param name="Height">原图像素高。</param>
    public sealed record AnalyzedScreenshot(
        IReadOnlyList<TextLine> Lines,
        Bitmap Overlay,
        GrayImage Gray,
        int Width,
        int Height);

    public static AnalyzedScreenshot Analyze(Bitmap source, TextLineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        PixelSize size = source.PixelSize;
        int width = size.Width;
        int height = size.Height;
        byte[] bgra = ReadBgra(source, width, height);

        GrayImage gray = ToGrayImage(bgra, width, height);
        IReadOnlyList<TextLine> lines = TextLineLocator.Locate(gray, options);

        byte[] overlayPixels = (byte[])bgra.Clone();
        TextLineOverlay.Draw(overlayPixels, width, height, lines);

        return new AnalyzedScreenshot(lines, CreateBitmap(overlayPixels, width, height), gray, width, height);
    }

    private static byte[] ReadBgra(Bitmap source, int width, int height)
    {
        var buffer = new byte[width * height * 4];

        // CopyPixels 只接受 ILockedFramebuffer，借一张临时位图当缓冲用。
        using WriteableBitmap staging = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (ILockedFramebuffer framebuffer = staging.Lock())
        {
            source.CopyPixels(framebuffer);

            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(
                    IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                    buffer,
                    y * width * 4,
                    width * 4);
            }
        }

        return buffer;
    }

    private static GrayImage ToGrayImage(byte[] bgra, int width, int height)
    {
        var gray = new byte[width * height];

        for (int i = 0; i < gray.Length; i++)
        {
            int index = i * 4;
            double blue = bgra[index];
            double green = bgra[index + 1];
            double red = bgra[index + 2];
            double alpha = bgra[index + 3] / 255.0;

            // BT.601 亮度权重；带透明通道的图先合成到黑底上，避免读到无意义的底色。
            double luminance = ((red * 299) + (green * 587) + (blue * 114)) / 1000.0;
            gray[i] = (byte)Math.Clamp(luminance * alpha, 0, 255);
        }

        return new GrayImage(width, height, gray);
    }

    private static WriteableBitmap CreateBitmap(byte[] bgra, int width, int height)
    {
        WriteableBitmap bitmap = new(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using (ILockedFramebuffer framebuffer = bitmap.Lock())
        {
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(
                    bgra,
                    y * width * 4,
                    IntPtr.Add(framebuffer.Address, y * framebuffer.RowBytes),
                    width * 4);
            }
        }

        return bitmap;
    }
}
