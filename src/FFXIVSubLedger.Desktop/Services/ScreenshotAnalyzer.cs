using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FFXIVSubLedger.Infrastructure.Recognition;

namespace FFXIVSubLedger.Desktop.Services;

/// <summary>
/// 把 Avalonia 位图和识别算法接起来：读像素 → 检测网格 → 生成叠加预览图。
///
/// 会创建 WriteableBitmap，所以必须在 UI 线程上调用。收艇截图通常只有几百像素见方，
/// 整个流程在毫秒级，直接同步跑不会卡界面。
/// </summary>
internal static class ScreenshotAnalyzer
{
    /// <summary>检测结果与叠加预览图。</summary>
    /// <param name="Grid">网格检测结果，没检出时为 null。</param>
    /// <param name="Overlay">把网格画上去的预览图，没检出时为 null。</param>
    /// <param name="Width">原图像素宽。</param>
    /// <param name="Height">原图像素高。</param>
    public sealed record AnalyzedScreenshot(
        GridDetectionResult? Grid,
        Bitmap? Overlay,
        int Width,
        int Height);

    public static AnalyzedScreenshot Analyze(Bitmap source, GridDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        PixelSize size = source.PixelSize;
        int width = size.Width;
        int height = size.Height;
        byte[] bgra = ReadBgra(source, width, height);

        GridDetectionResult? grid = GridDetector.Detect(ToGrayImage(bgra, width, height), options);
        if (grid is null)
            return new AnalyzedScreenshot(null, null, width, height);

        byte[] overlayPixels = (byte[])bgra.Clone();
        GridOverlay.Draw(overlayPixels, width, height, grid);

        return new AnalyzedScreenshot(grid, CreateBitmap(overlayPixels, width, height), width, height);
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
