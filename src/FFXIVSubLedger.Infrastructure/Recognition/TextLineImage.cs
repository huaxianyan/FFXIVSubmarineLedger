namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 预处理后的单行图像：白底黑字，供 OCR 引擎直接使用。
/// </summary>
public sealed class TextLineImage
{
    public TextLineImage(int width, int height, byte[] pixels)
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

    /// <summary>逐行排列，每像素 1 字节；0 为文字，255 为背景。</summary>
    public byte[] Pixels { get; }
}
