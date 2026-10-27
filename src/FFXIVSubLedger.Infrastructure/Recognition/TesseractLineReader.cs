using Tesseract;

namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 用 Tesseract 读单行日文。引擎与模型随程序分发，不依赖系统安装的语言包。
/// </summary>
public sealed class TesseractLineReader : IDisposable
{
    private readonly TesseractEngine _engine;

    /// <param name="tessdataDirectory">含 <c>jpn.traineddata</c> 的目录。</param>
    /// <param name="language">训练数据文件名前缀，默认日文。</param>
    public TesseractLineReader(string tessdataDirectory, string language = "jpn")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tessdataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        _engine = new TesseractEngine(tessdataDirectory, language, EngineMode.LstmOnly);
        _engine.DefaultPageSegMode = PageSegMode.SingleLine;
    }

    public string Read(TextLineImage line)
    {
        ArgumentNullException.ThrowIfNull(line);

        byte[] bmp = EncodeBmp(line);
        using Pix pix = Pix.LoadFromMemory(bmp);
        using Page page = _engine.Process(pix, PageSegMode.SingleLine);

        return page.GetText().Trim();
    }

    public void Dispose() => _engine.Dispose();

    /// <summary>
    /// 编一张 24 位 BMP。Tesseract 的图像入口吃 BMP/PNG 字节流，BMP 不用引图像库。
    /// </summary>
    private static byte[] EncodeBmp(TextLineImage image)
    {
        int rowSize = ((image.Width * 3) + 3) / 4 * 4;
        int pixelBytes = rowSize * image.Height;
        int fileSize = 54 + pixelBytes;

        var buffer = new byte[fileSize];
        buffer[0] = (byte)'B';
        buffer[1] = (byte)'M';
        WriteInt32(buffer, 2, fileSize);
        WriteInt32(buffer, 10, 54);
        WriteInt32(buffer, 14, 40);
        WriteInt32(buffer, 18, image.Width);
        WriteInt32(buffer, 22, image.Height);
        WriteInt16(buffer, 26, 1);
        WriteInt16(buffer, 28, 24);
        WriteInt32(buffer, 34, pixelBytes);

        for (int y = 0; y < image.Height; y++)
        {
            // BMP 自下而上存行。
            int rowStart = 54 + ((image.Height - 1 - y) * rowSize);
            for (int x = 0; x < image.Width; x++)
            {
                byte value = image.Pixels[(y * image.Width) + x];
                int offset = rowStart + (x * 3);
                buffer[offset] = value;
                buffer[offset + 1] = value;
                buffer[offset + 2] = value;
            }
        }

        return buffer;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteInt16(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }
}
