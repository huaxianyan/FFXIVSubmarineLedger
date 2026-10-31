using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using NavigationPage = FFXIVSubmarineLedger.Desktop.ViewModels.NavigationPage;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 主窗口的骨架：导航顺序、筛选栏的位置。
///
/// 筛选栏是标题栏里的一行，收艇记录页和收益统计页都有、正文里没有。
/// 这种事靠读代码猜容易走眼，直接看渲染后的控件树。
/// </summary>
public sealed class ShellLayoutTests
{
    [AvaloniaFact]
    public void StatisticsComesBeforeTheLedgerPage()
    {
        (Window window, MainWindowViewModel shell) = Show();

        List<string> labels =
        [
            .. window.GetVisualDescendants().OfType<Button>()
                .Where(x => x.Classes.Contains("nav"))
                .Select(x => x.Content?.ToString() ?? string.Empty),
        ];

        Assert.Equal(
            ["收艇记账", "收益统计", "收艇记录", "角色管理", "设置"],
            labels);

        window.Close();
        _ = shell;
    }

    [AvaloniaTheory]
    [InlineData(NavigationPage.Ledger)]
    [InlineData(NavigationPage.Statistics)]
    public void FilterSitsInTheTitleBar(NavigationPage page)
    {
        (Window window, MainWindowViewModel shell) = Show();
        shell.CurrentPage = page;
        Dispatcher.UIThread.RunJobs();

        // 标题栏 = 包着页面标题的那个 Border。筛选的两个下拉都该在它里面。
        TextBlock title = window.GetVisualDescendants().OfType<TextBlock>()
            .First(x => x.Classes.Contains("page-title"));
        Border header = title.GetVisualAncestors().OfType<Border>().First();

        List<ComboBox> filters =
        [
            .. window.GetVisualDescendants().OfType<ComboBox>()
                .Where(x => x.Bounds.Width > 0),
        ];

        Assert.Equal(2, filters.Count);
        foreach (ComboBox filter in filters)
        {
            Assert.Contains(filter, header.GetVisualDescendants().OfType<ComboBox>());
        }

        window.Close();
    }

    [AvaloniaFact]
    public void OtherPagesHaveNoFilter()
    {
        (Window window, MainWindowViewModel shell) = Show();
        shell.CurrentPage = NavigationPage.Characters;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<ComboBox>(),
            x => x.Bounds.Width > 0);

        window.Close();
    }

    private static (Window Window, MainWindowViewModel Shell) Show()
    {
        var shell = new MainWindowViewModel(ApplicationComposition.Create(), () => null);
        var window = new Views.MainWindow { DataContext = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, shell);
    }
}
