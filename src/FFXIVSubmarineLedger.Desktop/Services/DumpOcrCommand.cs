using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;
using FFXIVSubmarineLedger.Infrastructure.Recognition;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 开发期命令：不开窗口，把每条文本行的 OCR 结果写进文本文件，用来量识别准确率。
///
/// 用法：`FFXIVSubmarineLedger.exe --dump-ocr &lt;图片&gt; &lt;输出.txt&gt; &lt;tessdata 目录&gt;`。
/// </summary>
internal static class DumpOcrCommand
{
    public static bool TryParse(string[] args, out string imagePath, out string outputPath, out string tessdataPath)
    {
        imagePath = string.Empty;
        outputPath = string.Empty;
        tessdataPath = string.Empty;

        if (args.Length < 3 || args[0] is not "--dump-ocr")
            return false;

        imagePath = args[1];
        outputPath = args[2];

        // 不传就用程序目录下的模型，与界面走同一条解析路径。
        tessdataPath = args.Length >= 4 ? args[3] : string.Empty;
        return true;
    }

    public static void Run(string imagePath, string outputPath, string tessdataPath)
    {
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();

        using var bitmap = new Bitmap(imagePath);
        ScreenshotAnalyzer.AnalyzedScreenshot analyzed = ScreenshotAnalyzer.Analyze(bitmap);

        var text = new System.Text.StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"行数 {analyzed.Lines.Count}");

        var recognized = new List<string>(analyzed.Lines.Count);
        using var reader = new TesseractLineReader(TessdataLocator.Resolve(tessdataPath));
        foreach (TextLine line in analyzed.Lines)
        {
            TextLineImage processed = LinePreprocessor.Process(analyzed.Gray, line);
            string lineText = reader.Read(processed).ReplaceLineEndings(" ");
            recognized.Add(lineText);

            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"#{line.Index + 1:00} | {lineText}");
        }

        // 顺手把识别出来的入手行也列上，方便一次命令核完整条链。
        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(recognized, ItemCatalog.Load());
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"入手 {loot.Count} 条");

        foreach (RecognizedLoot entry in loot)
        {
            string name = entry.IsHighQuality ? $"{entry.ItemName}(HQ)" : entry.ItemName;
            string submarine = entry.SubmarineName is { Length: > 0 } sub ? $"[{sub}] " : "[未标明艇] ";
            string suffix = entry.IsKnown ? string.Empty : "  [未命中词表]";
            if (entry.IsAmbiguous)
                suffix += $"  [候选 {entry.Candidates.Count}: {string.Join(" / ", entry.Candidates.Select(x => x.CanonicalName))}]";

            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"#{entry.LineNumber:00} {submarine}{name} ×{entry.Quantity} @{entry.UnitPrice} = {entry.Subtotal}{suffix}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"合计 {loot.Sum(x => x.Subtotal)}");

        File.WriteAllText(outputPath, text.ToString());
    }
}
