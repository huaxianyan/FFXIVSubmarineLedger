using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using FFXIVSubmarineLedger.Desktop.Views;
using FFXIVSubmarineLedger.Platform.Windows;

namespace FFXIVSubmarineLedger.Desktop;

// 这里必须写全限定名：本项目的 FFXIVSubmarineLedger.Application 命名空间会遮蔽 Avalonia.Application。
public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 先建窗口、先让窗口显示出来，再去打开数据库。
            // 库里被别的进程占着时（比如上次的实例没退干净），开库会等锁——那段时间窗口还不在，
            // 用户看到的就是「进程在跑，界面不出来」。把顺序倒过来，至少界面先出现、状态栏能说话。
            MainWindow window = new();
            desktop.MainWindow = window;

            window.Opened += async (_, _) =>
            {
                StartupDiagnostics.Record("窗口已显示，开始初始化");

                ApplicationComposition composition;
                MainWindowViewModel viewModel;

                try
                {
                    composition = await Task.Run(ApplicationComposition.Create).ConfigureAwait(true);
                    composition.Database.Initialize();
                    StartupDiagnostics.Record("数据库就绪");
                }
                catch (Exception exception)
                {
                    StartupDiagnostics.RecordException("初始化数据", exception);
                    StartupDiagnostics.ShowError(
                        "FFXIV Submarine Ledger",
                        $"启动失败，数据没能读出来。\n\n{exception.Message}\n\n日志：{StartupDiagnostics.LogPath}");
                    desktop.Shutdown(1);
                    return;
                }

                try
                {
                    // 页面要用到剪贴板和文件选择器，这些只有窗口建出来之后才拿得到，所以传一个延迟取值。
                    viewModel = new MainWindowViewModel(composition, () => window);
                    window.DataContext = viewModel;

                    await viewModel.InitializeAsync().ConfigureAwait(true);

                    // 开发期方便：--image <路径> 启动时直接灌一张截图，省得反复手动拖图调检测。
                    if (PreloadImagePath(desktop.Args) is { } preload)
                        viewModel.Records.LoadFromPath(preload);
                }
                catch (Exception exception)
                {
                    StartupDiagnostics.RecordException("加载界面数据", exception);
                    StartupDiagnostics.ShowError(
                        "FFXIV Submarine Ledger",
                        $"界面数据加载失败。\n\n{exception.Message}\n\n日志：{StartupDiagnostics.LogPath}");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string? PreloadImagePath(string[]? args)
    {
        if (args is null)
            return null;

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] is "--image" or "-i")
                return args[index + 1];
        }

        return null;
    }
}
