namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// RGB 三通道图像，每像素 3 字节，顺序 R、G、B。
///
/// 图标匹配必须带颜色：背包里的宝石按颜色区分，转成灰度后钻石、绿宝石、红宝石会长得差不多，
/// 匹配无从下手。所以网格检测用 <see cref="GrayImage"/>，图标匹配用这个。
/// </summary>
public sealed class RgbImage
{
    public RgbImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);

        int expected = width * height * 3;
        if (pixels.Length != expected)
            throw new ArgumentException($"像素数据长度应为 {expected}，实际 {pixels.Length}。", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>逐像素排列，每像素 3 字节：R、G、B。</summary>
    public byte[] Pixels { get; }

    /// <summary>取某个像素的通道值，越界返回 0。</summary>
    public byte At(int x, int y, int channel)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return 0;

        return Pixels[(((y * Width) + x) * 3) + channel];
    }
}
