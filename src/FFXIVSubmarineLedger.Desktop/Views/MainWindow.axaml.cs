using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using FFXIVSubmarineLedger.Platform.Windows;

namespace FFXIVSubmarineLedger.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => EnsureOnScreen();
    }

    /// <summary>
    /// 确保窗口落在某个真实屏幕上。多显示器拔掉、分辨率或缩放改过之后，窗口可能被摆到看不见的地方——
    /// 那种情况在任务管理器里看是「进程在跑」，屏幕上什么都没有。这里把窗口拉回主屏中央。
    /// </summary>
    private void EnsureOnScreen()
    {
        try
        {
            Screen? target = Screens.Primary ?? Screens.All.FirstOrDefault();
            if (target is null)
                return;

            PixelRect work = target.WorkingArea;

            // 屏幕上至少要有这么大一块落在工作区内，否则就当作跑到外面去了。
            const int minimumVisible = 80;

            bool visible =
                Position.X + minimumVisible < work.Right
                && Position.X + Width - minimumVisible > work.X
                && Position.Y + minimumVisible < work.Bottom
                && Position.Y + Height - minimumVisible > work.Y;

            if (visible)
                return;

            StartupDiagnostics.Record(
                $"窗口位置 ({Position.X}, {Position.Y}) 不在屏幕 {work} 内，已移回中央");

            double width = double.IsNaN(Width) ? 1400 : Width;
            double height = double.IsNaN(Height) ? 900 : Height;

            Position = new PixelPoint(
                work.X + (int)Math.Max(0, (work.Width - width) / 2),
                work.Y + (int)Math.Max(0, (work.Height - height) / 2));
        }
        catch (Exception exception)
        {
            // 位置修正失败不该阻止窗口显示，记一笔就够。
            StartupDiagnostics.RecordException("修正窗口位置", exception);
        }
    }
}
