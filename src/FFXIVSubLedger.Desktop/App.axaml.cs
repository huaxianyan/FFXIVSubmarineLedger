using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FFXIVSubLedger.Desktop.ViewModels;
using FFXIVSubLedger.Desktop.Views;

namespace FFXIVSubLedger.Desktop;

// 这里必须写全限定名：本项目的 FFXIVSubLedger.Application 命名空间会遮蔽 Avalonia.Application。
public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ApplicationComposition composition = ApplicationComposition.Create();
            composition.Database.Initialize();

            // 页面要用到剪贴板和文件选择器，这些只有窗口建出来之后才拿得到，所以传一个延迟取值。
            MainWindow window = new();
            MainWindowViewModel viewModel = new(composition, () => window);
            window.DataContext = viewModel;
            desktop.MainWindow = window;

            // 首屏数据在窗口显示后异步加载，避免启动时阻塞 UI 线程。
            _ = viewModel.InitializeAsync();

            // 开发期方便：--image <路径> 启动时直接灌一张截图，省得反复手动拖图调检测。
            if (PreloadImagePath(desktop.Args) is { } preload)
                window.Opened += (_, _) => viewModel.Records.LoadFromPath(preload);
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
