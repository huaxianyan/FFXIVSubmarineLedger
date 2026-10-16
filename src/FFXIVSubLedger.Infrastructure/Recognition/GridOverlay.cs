namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 把检测出的网格画到 BGRA 像素上，用于给用户核对对齐是否正确。
/// 只对字节数组操作，不依赖任何 UI 类型。
/// </summary>
public static class GridOverlay
{
    /// <summary>竖线用偏红，方便和横线一眼区分开。</summary>
    private static readonly Rgb VerticalColour = new(0xFF, 0x3B, 0x5C);

    /// <summary>横线用偏青。</summary>
    private static readonly Rgb HorizontalColour = new(0x2B, 0xE3, 0xFF);

    /// <summary>空位压暗后剩下的亮度比例。</summary>
    private const double EmptyCellDimming = 0.5;

    /// <param name="bgra">就地修改的 BGRA 像素，长度必须等于 width × height × 4。</param>
    public static void Draw(byte[] bgra, int width, int height, GridDetectionResult grid)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        ArgumentNullException.ThrowIfNull(grid);

        if (bgra.Length != width * height * 4)
        {
            throw new ArgumentException(
                $"像素缓冲区长度 {bgra.Length} 与 {width} × {height} × 4 对不上。",
                nameof(bgra));
        }

        // 先压暗空位再画线，线就不会被压暗盖住。
        foreach (GridCell cell in grid.Cells)
        {
            if (cell.IsEmpty)
                Dim(bgra, width, cell);
        }

        foreach (int x in grid.VerticalLines)
            DrawVerticalLine(bgra, width, height, x, VerticalColour);

        foreach (int y in grid.HorizontalLines)
            DrawHorizontalLine(bgra, width, height, y, HorizontalColour);
    }

    private static void Dim(byte[] bgra, int width, GridCell cell)
    {
        for (int y = cell.Y; y < cell.Y + cell.Height; y++)
        {
            int rowStart = y * width;
            for (int x = cell.X; x < cell.X + cell.Width; x++)
            {
                int index = (rowStart + x) * 4;
                bgra[index] = (byte)(bgra[index] * EmptyCellDimming);
                bgra[index + 1] = (byte)(bgra[index + 1] * EmptyCellDimming);
                bgra[index + 2] = (byte)(bgra[index + 2] * EmptyCellDimming);
            }
        }
    }

    private static void DrawVerticalLine(byte[] bgra, int width, int height, int x, Rgb colour)
    {
        if (x < 0 || x >= width)
            return;

        for (int y = 0; y < height; y++)
            SetPixel(bgra, ((y * width) + x) * 4, colour);
    }

    private static void DrawHorizontalLine(byte[] bgra, int width, int height, int y, Rgb colour)
    {
        if (y < 0 || y >= height)
            return;

        int rowStart = y * width;
        for (int x = 0; x < width; x++)
            SetPixel(bgra, (rowStart + x) * 4, colour);
    }

    /// <summary>缓冲区是 BGRA 字节序，写的时候别把红蓝弄反。</summary>
    private static void SetPixel(byte[] bgra, int index, Rgb colour)
    {
        bgra[index] = colour.Blue;
        bgra[index + 1] = colour.Green;
        bgra[index + 2] = colour.Red;
        bgra[index + 3] = 0xFF;
    }

    private readonly record struct Rgb(byte Red, byte Green, byte Blue);
}
