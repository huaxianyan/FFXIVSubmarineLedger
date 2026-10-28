using Avalonia;
using FFXIVSubLedger.Desktop.Services;

namespace FFXIVSubLedger.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 开发期命令先拦，拦到就不进 UI 循环。
        if (DumpLinesCommand.TryParse(args, out string imagePath, out string outputPath))
        {
            DumpLinesCommand.Run(imagePath, outputPath);
            return;
        }

        if (DumpOcrCommand.TryParse(args, out string ocrImage, out string ocrOutput, out string tessdata))
        {
            DumpOcrCommand.Run(ocrImage, ocrOutput, tessdata);
            return;
        }

        if (DumpChatDiagCommand.TryParse(args, out string diagOutput))
        {
            DumpChatDiagCommand.Run(diagOutput);
            return;
        }

        if (DumpChatCommand.TryParse(args, out string chatOutput))
        {
            DumpChatCommand.Run(chatOutput);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
