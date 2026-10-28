using Avalonia.Controls;
using FFXIVSubLedger.Desktop.ViewModels;

namespace FFXIVSubLedger.Desktop.Views;

public partial class SettingsPageView : UserControl
{
    public SettingsPageView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsPageViewModel viewModel)
        {
            viewModel.EnsureLanguageSelected();
            viewModel.EnsureCatalogLoaded();
        }
    }
}
