using Avalonia;
using FFXIVSubmarineLedger.Desktop.Services;
using FFXIVSubmarineLedger.Platform.Windows;

namespace FFXIVSubmarineLedger.Desktop;

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

        // 已经有实例在跑就把它叫到前台，别开第二个进程抢同一个库。
        using var instance = new SingleInstance();
        if (!instance.TryAcquire())
        {
            StartupDiagnostics.Record("已有实例在运行，把它的窗口叫到前台");
            SingleInstance.ActivateExistingWindow("FFXIV Submarine Ledger");
            return;
        }

        StartupDiagnostics.Record($"启动主进程，参数：{string.Join(' ', args)}");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.RecordException("界面启动", exception);
            StartupDiagnostics.ShowError(
                "FFXIV Submarine Ledger",
                $"程序启动失败。\n\n{exception.Message}\n\n日志：{StartupDiagnostics.LogPath}");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
