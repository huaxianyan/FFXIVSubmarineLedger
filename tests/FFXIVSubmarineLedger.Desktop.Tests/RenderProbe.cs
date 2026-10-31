using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 把真实视图渲染成 PNG 存盘，供人工核对排版。
/// 无头渲染不开窗口、不抢焦点，不会打断用户正在做的事。
///
/// 只在设了 <c>PROBE_OUT</c> 时才干活：平时的测试跑不写文件。
/// </summary>
public sealed class RenderProbe
{
    [AvaloniaFact]
    public async Task RenderPages()
    {
        string? output = Environment.GetEnvironmentVariable("PROBE_OUT");
        if (string.IsNullOrWhiteSpace(output))
            return;

        string stem = Path.GetFileNameWithoutExtension(output);
        string directory = Path.GetDirectoryName(output)!;

        LedgerPageViewModel ledger = new(
            Sample.RecordRepository, Sample.SubmarineRepository, Sample.Characters, () => null,
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
        await ledger.LoadAsync();
        Render(new Views.LedgerPageView { DataContext = ledger }, 1200, 700,
            Path.Combine(directory, $"{stem}-ledger.png"));

        StatisticsPageViewModel statistics = new(
            Sample.RecordRepository, Sample.SubmarineRepository, Sample.Characters, () => null,
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
        await statistics.LoadAsync();
        Render(new Views.StatisticsPageView { DataContext = statistics }, 1200, 1400,
            Path.Combine(directory, $"{stem}-statistics.png"));
    }

    private static void Render(Control view, int width, int height, string path)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        using (var stream = File.Create(path))
            frame.Save(stream, new PngBitmapEncoderOptions());

        window.Close();
    }
}
