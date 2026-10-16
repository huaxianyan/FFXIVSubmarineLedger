using System.Text;
using FFXIVSubLedger.Infrastructure.Recognition;

namespace FFXIVSubLedger.Domain.Tests.Recognition;

/// <summary>
/// 用一张真实收艇截图（371×177，用户实际截图尺度）跑网格检测。
///
/// 为什么非要留真实样本：合成图能把边界造得干干净净，测不出「截图贴着最后一格右边缘裁」
/// 这类真实约束。5 列被算成 4 列那个 bug 合成测试全绿，只有这张真实样本抓到了。
///
/// 样本是灰度 PGM（P5），没有压缩，所以测试不依赖任何图像库。
/// </summary>
public sealed class RealBagSampleTests
{
    /// <summary>样本真值：5 列 × 2 行，第一行 5 个道具，第二行前 2 个有、后 3 个空。</summary>
    private const int ExpectedCellSize = 69;
    private const int ExpectedColumns = 5;
    private const int ExpectedRows = 2;
    private const int ExpectedOccupied = 7;
    private const int ExpectedEmpty = 3;

    [Fact]
    public void Detect_MatchesGroundTruthOnRealScreenshot()
    {
        GrayImage image = LoadSample("bag-371x177.pgm");

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.Equal(ExpectedCellSize, grid.CellSize);
        Assert.Equal(ExpectedColumns, grid.Columns);
        Assert.Equal(ExpectedRows, grid.Rows);
        Assert.True(grid.IsConfident, $"真实样本不该判为不可信：相关度 {grid.ColumnCorrelation:F3}，锐度 {grid.PhaseSharpness:F2}");
    }

    [Fact]
    public void Detect_ReachesBothHorizontalEdgesOnRealScreenshot()
    {
        // 这张截图左右贴着格子边缘裁，所以首尾两条竖线要顶到画布两端。
        // 上下不是贴边裁的：底部还露出下一行约 10 px，那一行不完整，不该被算进来。
        GrayImage image = LoadSample("bag-371x177.pgm");

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.InRange(grid.VerticalLines[0], 22, 30);
        Assert.Equal(image.Width, grid.VerticalLines[^1]);

        Assert.InRange(grid.HorizontalLines[0], 25, 33);
        Assert.Equal(3, grid.HorizontalLines.Count);
        Assert.InRange(grid.HorizontalLines[^1], image.Height - 16, image.Height - 4);
    }

    [Fact]
    public void Detect_SeparatesItemsFromEmptySlotsOnRealScreenshot()
    {
        GrayImage image = LoadSample("bag-371x177.pgm");

        GridDetectionResult grid = Assert.IsType<GridDetectionResult>(GridDetector.Detect(image));

        Assert.Equal(ExpectedOccupied, grid.OccupiedCount);
        Assert.Equal(ExpectedEmpty, grid.EmptyCount);

        // 第二行右边三格是空的，左上角那格一定有道具。
        Assert.False(CellAt(grid, 0, 0).IsEmpty);
        Assert.False(CellAt(grid, 1, 1).IsEmpty);
        Assert.True(CellAt(grid, 2, 1).IsEmpty);
        Assert.True(CellAt(grid, 3, 1).IsEmpty);
        Assert.True(CellAt(grid, 4, 1).IsEmpty);
    }

    private static GridCell CellAt(GridDetectionResult grid, int column, int row) =>
        grid.Cells.Single(cell => cell.Column == column && cell.Row == row);

    /// <summary>读 PGM（P5 二进制灰度）。样本随测试一起复制到输出目录。</summary>
    private static GrayImage LoadSample(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Recognition", "Samples", fileName);
        byte[] raw = File.ReadAllBytes(path);

        // 头部：魔数 P5、宽、高、最大值，四段之间用任意空白分隔（换行或空格都合法），
        // 解析完紧跟一个字节的空白，之后就是像素数据。
        int cursor = 0;
        Assert.Equal("P5", ReadToken(raw, ref cursor));
        int width = int.Parse(ReadToken(raw, ref cursor));
        int height = int.Parse(ReadToken(raw, ref cursor));
        Assert.Equal("255", ReadToken(raw, ref cursor));
        cursor++;

        byte[] pixels = raw[cursor..];
        Assert.Equal(width * height, pixels.Length);

        return new GrayImage(width, height, pixels);
    }

    private static string ReadToken(byte[] raw, ref int cursor)
    {
        while (cursor < raw.Length && char.IsWhiteSpace((char)raw[cursor]))
            cursor++;

        int start = cursor;
        while (cursor < raw.Length && !char.IsWhiteSpace((char)raw[cursor]))
            cursor++;

        return Encoding.ASCII.GetString(raw, start, cursor - start);
    }
}
