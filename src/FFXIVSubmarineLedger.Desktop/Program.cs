using Avalonia;
using Avalonia.Win32;
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
        if (!instance.TryAcquire() && !WaitForExistingWindow())
        {
            // 锁被占着却等不到窗口：那个实例卡住了。
            // 这里**不能静默退出**——用户会以为双击没反应，而实际上什么都没发生。
            // 与其让人对着一个看不见的进程发呆，不如再起一个实例：数据库那头有 5 秒超时兜底。
            StartupDiagnostics.Record("已有实例占着锁但等不到窗口，改为启动新实例");
        }

        StartupDiagnostics.Record($"启动主进程 pid={Environment.ProcessId}，参数：{string.Join(' ', args)}");

        // 看门狗：界面若迟迟不出现，至少让人看到一句话，而不是「双击没反应」。
        StartWindowWatchdog();

        try
        {
            StartupDiagnostics.Record("开始构建 AppBuilder");
            AppBuilder builder = BuildAvaloniaApp();
            StartupDiagnostics.Record($"AppBuilder 已构建（渲染={DescribeRendering()}）");

            builder.StartWithClassicDesktopLifetime(args);
            StartupDiagnostics.Record("UI 循环结束");
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

    /// <summary>
    /// 等已有实例把窗口亮出来。慢启动（比如杀毒软件正在扫）要给它一点时间，
    /// 所以不是一次找不到就判死刑，而是等 8 秒。
    /// </summary>
    private static bool WaitForExistingWindow()
    {
        for (int waited = 0; waited < 8; waited++)
        {
            if (SingleInstance.ActivateExistingWindow("FFXIV Submarine Ledger"))
            {
                StartupDiagnostics.Record("已有实例在运行，把它的窗口叫到前台");
                return true;
            }

            Thread.Sleep(1000);
        }

        return false;
    }

    /// <summary>
    /// 启动看门狗。界面正常时会在几秒内出现，这时看门狗自己退出；
    /// 超过时限还没出现，就弹框说明卡住了并给出日志位置。
    /// </summary>
    private static void StartWindowWatchdog()
    {
        var watchdog = new Thread(() =>
        {
            for (int waited = 0; waited < 20; waited++)
            {
                Thread.Sleep(1000);

                if (SingleInstance.ActivateExistingWindow("FFXIV Submarine Ledger"))
                {
                    // 窗口出来了（顺便把它带到前台），看门狗没事了。
                    return;
                }
            }

            StartupDiagnostics.Record("看门狗：20 秒内没有出现主窗口");
            StartupDiagnostics.ShowError(
                "FFXIV Submarine Ledger",
                $"程序启动了，但 20 秒内没有出现主窗口。\n\n"
                + $"日志：{StartupDiagnostics.LogPath}\n\n"
                + "可以把这个日志文件发给我，里面记录了启动走到了哪一步。");
        })
        {
            IsBackground = true,
            Name = "startup-watchdog",
        };

        watchdog.Start();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                // 默认先试硬件渲染，失败再退软件。显卡驱动初始化**挂住**（而不是报错）时
                // Avalonia 不会自动降级，表现就是「进程在跑、界面不出来」，所以留一个开关。
                RenderingMode = UseSoftwareRendering()
                    ? [Win32RenderingMode.Software]
                    : [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
            })
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// 是否强制软件渲染。默认走硬件渲染，只在显卡驱动或字体替换工具（MacType 之类）捣乱时用它绕过：
    /// 命令行加 <c>--software-render</c>，或设环境变量 <c>FFXIVSUB_SOFTWARE_RENDER=1</c>。
    /// </summary>
    private static bool UseSoftwareRendering()
    {
        if (Environment.GetEnvironmentVariable("FFXIVSUB_SOFTWARE_RENDER") is "1" or "true")
            return true;

        return Environment.GetCommandLineArgs().Contains("--software-render", StringComparer.OrdinalIgnoreCase);
    }

    private static string DescribeRendering() => UseSoftwareRendering() ? "software" : "hardware";
}
