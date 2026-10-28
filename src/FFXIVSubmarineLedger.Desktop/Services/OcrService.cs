using FFXIVSubmarineLedger.Infrastructure.Recognition;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 把行定位、预处理和 OCR 串起来，供界面调用。
///
/// Tesseract 引擎不是线程安全的，也不便宜，所以整个进程只留一个实例，访问串行化；
/// 识别本身放到线程池跑，界面线程只等结果。
/// </summary>
internal sealed class OcrService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TesseractLineReader? _reader;

    /// <summary>逐行读出文字，顺序与传入的行一致。识别失败的行给空串，不打断整批。</summary>
    public async Task<IReadOnlyList<string>> ReadLinesAsync(GrayImage image, IReadOnlyList<TextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(lines);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _reader ??= new TesseractLineReader(TessdataLocator.Resolve());

            return await Task.Run<IReadOnlyList<string>>(() =>
            {
                var texts = new List<string>(lines.Count);
                foreach (TextLine line in lines)
                {
                    TextLineImage processed = LinePreprocessor.Process(image, line);

                    try
                    {
                        texts.Add(_reader.Read(processed).ReplaceLineEndings(" "));
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        // 单行读失败不值得让整张图作废，界面上会显示成空行。
                        texts.Add(string.Empty);
                    }
                }

                return texts;
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _gate.Dispose();
    }
}
