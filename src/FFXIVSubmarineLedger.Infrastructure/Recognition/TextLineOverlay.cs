namespace FFXIVSubmarineLedger.Infrastructure.Recognition;

/// <summary>
/// 把行框画到 BGRA 缓冲区上，供预览核对。日志背景不是纯色，所以用高饱和色描边，
/// 不靠压暗背景来突出。
/// </summary>
public static class TextLineOverlay
{
    private const byte LineRed = 0x4D;
    private const byte LineGreen = 0xC3;
    private const byte LineBlue = 0xFF;

    public static void Draw(byte[] bgra, int width, int height, IReadOnlyList<TextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        ArgumentNullException.ThrowIfNull(lines);

        if (bgra.Length != width * height * 4)
            throw new ArgumentException($"像素数据长度应为 {width * height * 4}，实际 {bgra.Length}。", nameof(bgra));

        foreach (TextLine line in lines)
        {
            int left = Math.Max(0, line.Left - 2);
            int right = Math.Min(width - 1, line.Right + 2);
            int top = Math.Max(0, line.Top);
            int bottom = Math.Min(height - 1, line.Bottom);

            for (int x = left; x <= right; x++)
            {
                SetPixel(bgra, width, x, top);
                SetPixel(bgra, width, x, bottom);
            }

            for (int y = top; y <= bottom; y++)
            {
                SetPixel(bgra, width, left, y);
                SetPixel(bgra, width, right, y);
            }
        }
    }

    private static void SetPixel(byte[] bgra, int width, int x, int y)
    {
        int offset = ((y * width) + x) * 4;
        bgra[offset] = LineBlue;
        bgra[offset + 1] = LineGreen;
        bgra[offset + 2] = LineRed;
        bgra[offset + 3] = 255;
    }
}
