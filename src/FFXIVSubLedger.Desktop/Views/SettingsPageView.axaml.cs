using Avalonia.Input;
using Avalonia.Controls;
using FFXIVSubLedger.Desktop.ViewModels;

namespace FFXIVSubLedger.Desktop.Views;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView() => InitializeComponent();

    /// <summary>地址框失焦时才保存，避免每敲一个字写一次库。</summary>
    private void OnProxyUrlLostFocus(object? sender, FocusChangedEventArgs e) =>
        (DataContext as SettingsPageViewModel)?.SaveUpdateOptions();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsPageViewModel viewModel)
        {
            viewModel.EnsureLanguageSelected();
            viewModel.EnsureUpdateOptionsLoaded();
            viewModel.EnsureCatalogLoaded();
        }
    }
}
