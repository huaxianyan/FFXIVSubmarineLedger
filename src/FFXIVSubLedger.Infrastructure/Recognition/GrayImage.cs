namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 8 位灰度图。识别算法只吃亮度结构，不需要颜色，所以整条流水线都停在这一层。
/// </summary>
public sealed class GrayImage
{
    public GrayImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);

        if (pixels.Length != width * height)
            throw new ArgumentException($"像素数据长度应为 {width * height}，实际 {pixels.Length}。", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>逐行排列，每行 <see cref="Width"/> 字节。</summary>
    public byte[] Pixels { get; }

    public byte this[int x, int y] => Pixels[(y * Width) + x];
}
