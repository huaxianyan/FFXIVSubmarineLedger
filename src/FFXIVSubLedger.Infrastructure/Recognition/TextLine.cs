namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 一条日志文本行在图里的位置。坐标以整张截图为基准，<see cref="Right"/> 与 <see cref="Bottom"/> 都包含在内。
/// </summary>
/// <param name="Index">自上而下的序号，从 0 开始。</param>
/// <param name="Top">行带上边缘。</param>
/// <param name="Height">行带高度，含字符上下伸出的部分与描边。</param>
/// <param name="Left">行内最靠左的文字像素列。</param>
/// <param name="Right">行内最靠右的文字像素列。</param>
public readonly record struct TextLine(int Index, int Top, int Height, int Left, int Right)
{
    public int Bottom => Top + Height - 1;

    public int Width => Right - Left + 1;
}
