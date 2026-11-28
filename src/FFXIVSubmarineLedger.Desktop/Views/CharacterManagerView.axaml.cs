using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FFXIVSubmarineLedger.Desktop.ViewModels;

namespace FFXIVSubmarineLedger.Desktop.Views;

public partial class CharacterManagerView : UserControl
{
    public CharacterManagerView()
    {
        InitializeComponent();

        // 整页接管 Ctrl+V：焦点不在输入框里时也能把剪贴板里那串「名字@服务器」填进来。
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsVisible || e.Handled)
            return;

        // 焦点在文本框里时让 TextBox 自己粘，别抢它的。
        if (e.Source is TextBox)
            return;

        if (e.Key != Key.V || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        e.Handled = true;

        if (DataContext is CharacterManagerViewModel viewModel)
            viewModel.PasteCommand.Execute(null);
    }
}
