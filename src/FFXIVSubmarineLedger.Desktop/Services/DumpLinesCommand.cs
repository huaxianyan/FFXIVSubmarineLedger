using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using FFXIVSubmarineLedger.Infrastructure.Recognition;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 开发期命令：不开窗口，把一张截图切出的文本行写到文本文件里。
///
/// 用途是对真实样本核对行定位结果——行框画在预览图上靠肉眼看，几何数值靠这个对齐
/// 版本量出来的期望值。用法：`FFXIVSubmarineLedger.exe --dump-lines &lt;图片&gt; &lt;输出.txt&gt;`。
/// </summary>
internal static class DumpLinesCommand
{
    public static bool TryParse(string[] args, out string imagePath, out string outputPath)
    {
        imagePath = string.Empty;
        outputPath = string.Empty;

        if (args.Length < 3 || args[0] is not "--dump-lines")
            return false;

        imagePath = args[1];
        outputPath = args[2];
        return true;
    }

    public static void Run(string imagePath, string outputPath)
    {
        // 解码位图要走平台图像管线，所以只初始化平台、不建窗口。
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        using var bitmap = new Bitmap(imagePath);
        ScreenshotAnalyzer.AnalyzedScreenshot analyzed = ScreenshotAnalyzer.Analyze(bitmap);

        var text = new System.Text.StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"尺寸 {analyzed.Width} × {analyzed.Height}");
        text.AppendLine(CultureInfo.InvariantCulture, $"行数 {analyzed.Lines.Count}");

        foreach (TextLine line in analyzed.Lines)
        {
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"#{line.Index + 1:00} top={line.Top} 高={line.Height} 左={line.Left} 右={line.Right}");
        }

        File.WriteAllText(outputPath, text.ToString());
    }
}
