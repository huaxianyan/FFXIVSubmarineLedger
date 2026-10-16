using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FFXIVSubLedger.Desktop.ViewModels;

namespace FFXIVSubLedger.Desktop.Views;

public partial class RecordsPageView : UserControl
{
    private RecordsPageViewModel? _subscribed;

    public RecordsPageView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private RecordsPageViewModel? ViewModel => DataContext as RecordsPageViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribed is { } previous)
            previous.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribed = ViewModel;

        if (_subscribed is { } current)
            current.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Ctrl+V 挂在窗口上才稳：焦点落在页面里哪个控件上都照样生效。
        if (TopLevel.GetTopLevel(this) is { } topLevel)
            topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is { } topLevel)
            topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// 装进一张图之后把预览滚进视野。截图多半是「宽而扁」的背包区域，按容器宽度铺开以后
    /// 纵向会超出首屏，不滚一下用户就只能看到上半截。
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RecordsPageViewModel.HasImage))
            return;

        if (ViewModel?.HasImage != true)
            return;

        // 等这一轮布局跑完再滚，否则量到的还是旧尺寸。
        Dispatcher.UIThread.Post(() => PreviewPanel.BringIntoView(), DispatcherPriority.Background);
    }

    private async void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        // 本页没有任何文本输入框，所以只要页面可见，接管 Ctrl+V 不会影响别处。
        if (!IsVisible || e.Handled)
            return;

        if (e.Key != Key.V || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        e.Handled = true;

        if (ViewModel is { } viewModel)
            await viewModel.ReadFromClipboardAsync().ConfigureAwait(true);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool acceptable = e.DataTransfer is { } transfer
            && (transfer.Contains(DataFormat.Bitmap) || transfer.Contains(DataFormat.File));

        e.DragEffects = acceptable ? DragDropEffects.Copy : DragDropEffects.None;

        if (ViewModel is { } viewModel)
            viewModel.IsDragOver = acceptable;

        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (ViewModel is { } viewModel)
            viewModel.IsDragOver = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (ViewModel is not { } viewModel)
            return;

        viewModel.IsDragOver = false;
        await viewModel.AcceptDroppedAsync(e.DataTransfer).ConfigureAwait(true);
    }
}
