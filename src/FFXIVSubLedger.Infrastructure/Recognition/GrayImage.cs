namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 单通道灰度图。识别算法统一吃这个类型，这样算法层不需要依赖任何 UI 或图像库。
/// </summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Length != width * height)
        {
            throw new ArgumentException(
                $"像素数量 {pixels.Length} 与 {width} × {height} 对不上。",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>逐行排列的灰度值，长度等于 Width × Height。</summary>
    public byte[] Pixels { get; }

    public byte At(int x, int y) => Pixels[(y * Width) + x];
}
