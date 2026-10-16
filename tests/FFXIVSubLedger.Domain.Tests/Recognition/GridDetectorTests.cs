using FFXIVSubLedger.Infrastructure.Recognition;
using Xunit;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

public sealed class GridDetectorTests
{
    [Fact]
    public void Detect_RestoresCellSizeAndShape()
    {
        GrayImage image = SyntheticBag.Create(cellSize: 69, columns: 5, rows: 2, offsetX: 26, offsetY: 29);

        GridDetectionResult? grid = GridDetector.Detect(image);

        Assert.NotNull(grid);
        Assert.Equal(69, grid.CellSize);
        Assert.Equal(5, grid.Columns);
        Assert.Equal(2, grid.Rows);
        Assert.True(grid.IsConfident, $"不该判为不可信：相关度 {grid.ColumnCorrelation:F3}，锐度 {grid.PhaseSharpness:F3}");
    }

    [Fact]
    public void Detect_AlignsGridLinesToCellBoundaries()
    {
        GrayImage image = SyntheticBag.Create(cellSize: 69, columns: 5, rows: 2, offsetX: 26, offsetY: 29);

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        // 格子边框本身有宽度，检出的相位落在边框的某一侧，允许几像素偏差。
        Assert.InRange(grid.VerticalLines[0], 26 - 4, 26 + 4);
        Assert.InRange(grid.HorizontalLines[0], 29 - 4, 29 + 4);

        // 线间距必须严格等于边长，这是「正方形先验」的核心断言。
        for (int i = 1; i < grid.VerticalLines.Count; i++)
            Assert.Equal(69, grid.VerticalLines[i] - grid.VerticalLines[i - 1]);

        for (int i = 1; i < grid.HorizontalLines.Count; i++)
            Assert.Equal(69, grid.HorizontalLines[i] - grid.HorizontalLines[i - 1]);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(69)]
    [InlineData(96)]
    [InlineData(128)]
    public void Detect_HandlesDifferentUiScales(int cellSize)
    {
        // 格子边长随游戏 UI 倍率变化，检测必须动态得出，不能写死。
        GrayImage image = SyntheticBag.Create(cellSize, columns: 5, rows: 2, offsetX: 20, offsetY: 20);

        GridDetectionResult? grid = GridDetector.Detect(image);

        Assert.NotNull(grid);
        Assert.Equal(cellSize, grid.CellSize);
        Assert.Equal(5, grid.Columns);
        Assert.Equal(2, grid.Rows);
    }

    [Fact]
    public void Detect_CountsLastColumnWhenRightEdgeSitsOnGridLine()
    {
        // 截图常常贴着最后一格的右边缘裁，此时那条真正的边界正好等于图像宽度。
        // 早期版本用「严格小于」铺线，把这条边界丢掉，5 列被算成 4 列，最右一列道具整个丢失。
        GrayImage image = SyntheticBag.Create(
            cellSize: 69,
            columns: 5,
            rows: 2,
            offsetX: 26,
            offsetY: 29,
            marginRight: 0);

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.Equal(5, grid.Columns);
        Assert.Equal(2, grid.Rows);

        // 相位可能落在格子边框的任意一侧，允许几像素偏差，但必须贴近右边缘。
        Assert.InRange(grid.VerticalLines[^1], image.Width - 4, image.Width);
    }

    [Fact]
    public void Detect_DoesNotTurnNormalRightPaddingIntoAnExtraColumn()
    {
        // 正常的左右对称留白不该多算出一列。
        GrayImage image = SyntheticBag.Create(
            cellSize: 69,
            columns: 5,
            rows: 2,
            offsetX: 26,
            offsetY: 29,
            marginRight: 26);

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.Equal(5, grid.Columns);
    }

    [Fact]
    public void Detect_PrefersExtraEmptyColumnOverLosingARealOne()
    {
        // 右边留白刚好比一整格多几像素时，几何上无法和「一个空背包格」区分，多算一列是必然的。
        // 这里刻意把取舍钉下来：宁可多出一列空格子，也不能少算一列真格子——
        // 空格子不参与计价，代价只是显示上多一格；少一列则会丢掉整列道具。
        GrayImage image = SyntheticBag.Create(
            cellSize: 69,
            columns: 5,
            rows: 2,
            offsetX: 26,
            offsetY: 29,
            marginRight: 72);

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.Equal(6, grid.Columns);

        // 多出来的那一列必须是空的，不能把留白误报成道具。
        Assert.All(
            grid.Cells.Where(cell => cell.Column == 5),
            cell => Assert.True(cell.IsEmpty, "多出来的尾部列必须是空位"));
    }

    [Fact]
    public void Detect_MarksEmptyCells()
    {
        bool[] occupied =
        [
            true, true, false, true, true,
            false, true, true, true, false,
        ];

        GrayImage image = SyntheticBag.Create(69, 5, 2, 26, 29, occupied);

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.Equal(7, grid.OccupiedCount);
        Assert.Equal(3, grid.EmptyCount);

        Assert.False(CellAt(grid, 0, 0).IsEmpty);
        Assert.True(CellAt(grid, 2, 0).IsEmpty);
        Assert.True(CellAt(grid, 0, 1).IsEmpty);
        Assert.True(CellAt(grid, 4, 1).IsEmpty);
    }

    [Fact]
    public void Detect_ReturnsNullForBlankImage()
    {
        var pixels = new byte[200 * 200];
        Array.Fill(pixels, (byte)128);

        Assert.Null(GridDetector.Detect(new GrayImage(200, 200, pixels)));
    }

    [Fact]
    public void Detect_DoesNotClaimConfidenceOnNoise()
    {
        var random = new Random(20261014);
        var pixels = new byte[300 * 300];
        random.NextBytes(pixels);

        GridDetectionResult? grid = GridDetector.Detect(new GrayImage(300, 300, pixels));

        // 纯噪声偶尔会碰巧撞出一个周期，但不能被判成可信的对齐。
        Assert.True(grid is null || !grid.IsConfident);
    }

    [Fact]
    public void Detect_ReturnsNullWhenImageTooSmallForAnyCell()
    {
        var pixels = new byte[20 * 20];
        Array.Fill(pixels, (byte)40);

        Assert.Null(GridDetector.Detect(new GrayImage(20, 20, pixels)));
    }

    [Fact]
    public void Overlay_DrawsLinesInDifferentColoursPerDirection()
    {
        GrayImage image = SyntheticBag.Create(69, 5, 2, 26, 29);
        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        byte[] bgra = new byte[image.Width * image.Height * 4];
        GridOverlay.Draw(bgra, image.Width, image.Height, grid);

        int verticalX = grid.VerticalLines[1];
        int horizontalY = grid.HorizontalLines[1];

        // 缓冲区是 BGRA 字节序。竖线取 #FF3B5C、横线取 #2BE3FF，两者必须能区分开。
        int verticalIndex = (((horizontalY + 10) * image.Width) + verticalX) * 4;
        int horizontalIndex = ((horizontalY * image.Width) + verticalX + 10) * 4;

        Assert.Equal(0x5C, bgra[verticalIndex]);
        Assert.Equal(0x3B, bgra[verticalIndex + 1]);
        Assert.Equal(0xFF, bgra[verticalIndex + 2]);
        Assert.Equal(0xFF, bgra[verticalIndex + 3]);

        Assert.Equal(0xFF, bgra[horizontalIndex]);
        Assert.Equal(0xE3, bgra[horizontalIndex + 1]);
        Assert.Equal(0x2B, bgra[horizontalIndex + 2]);
        Assert.Equal(0xFF, bgra[horizontalIndex + 3]);
    }

    private static GridCell CellAt(GridDetectionResult grid, int column, int row) =>
        grid.Cells.Single(cell => cell.Column == column && cell.Row == row);
}

/// <summary>
/// 合成一张背包截图。格子边长、行列数、起点、哪些格子有道具都能精确控制，
/// 这样网格检测的测试就不依赖任何图片素材。
/// </summary>
internal static class SyntheticBag
{
    private const byte Outside = 20;
    private const byte CellBorder = 30;
    private const byte EmptyFill = 100;

    /// <summary>格子边框宽度。真实游戏里边框很细，且不随 UI 倍率线性放大。</summary>
    private const int BorderWidth = 2;

    public static GrayImage Create(
        int cellSize,
        int columns,
        int rows,
        int offsetX,
        int offsetY,
        bool[]? occupied = null,
        int seed = 20261014,
        int? marginRight = null)
    {
        if (occupied is not null && occupied.Length != columns * rows)
            throw new ArgumentException("占用标记的数量要和格子总数一致。", nameof(occupied));

        // 默认左右留白一样；传 marginRight: 0 可以造出「贴着最后一格右边缘裁」的截图。
        int rightMargin = marginRight ?? offsetX;
        int width = offsetX + (columns * cellSize) + rightMargin;
        int height = (offsetY * 2) + (rows * cellSize);
        var pixels = new byte[width * height];
        Array.Fill(pixels, Outside);

        var random = new Random(seed);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int x0 = offsetX + (column * cellSize);
                int y0 = offsetY + (row * cellSize);

                // 格子边框横竖贯穿整格，所以无论里面有没有道具，边界处都会留下等距的边缘。
                // 这就是网格检测要抓的信号。
                for (int y = 0; y < cellSize; y++)
                {
                    int rowStart = (y0 + y) * width;
                    bool onHorizontalBorder = y < BorderWidth || y >= cellSize - BorderWidth;

                    for (int x = 0; x < cellSize; x++)
                    {
                        bool onVerticalBorder = x < BorderWidth || x >= cellSize - BorderWidth;
                        pixels[rowStart + x0 + x] = onHorizontalBorder || onVerticalBorder
                            ? CellBorder
                            : EmptyFill;
                    }
                }

                bool filled = occupied is null || occupied[(row * columns) + column];
                if (!filled)
                    continue;

                // 道具图标铺满边框以内。这里刻意用低频平滑函数而不是逐像素随机数：
                // 真实道具图标是插画，不是噪声；用噪声会在格内制造大量杂乱边缘，
                // 把格子边界的信号整个淹掉，测出来的就不是网格检测的能力了。
                double phase = random.NextDouble() * Math.PI * 2;
                double half = (cellSize - (BorderWidth * 2)) / 2.0;

                for (int y = BorderWidth; y < cellSize - BorderWidth; y++)
                {
                    int rowStart = (y0 + y) * width;
                    double ny = (y - BorderWidth - half) / half;

                    for (int x = BorderWidth; x < cellSize - BorderWidth; x++)
                    {
                        double nx = (x - BorderWidth - half) / half;
                        double radius = Math.Min(1.0, Math.Sqrt((nx * nx) + (ny * ny)));

                        double value = 182
                            + (46 * Math.Sin((nx * 2.1) + phase) * Math.Cos((ny * 2.6) - phase))
                            - (34 * radius);

                        pixels[rowStart + x0 + x] = (byte)Math.Clamp(value, 0, 255);
                    }
                }
            }
        }

        return new GrayImage(width, height, pixels);
    }
}
