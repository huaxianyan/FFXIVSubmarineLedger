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

        // 本页没有「粘贴」按钮：Ctrl+V 就是把「角色名@服务器」填进来的入口。
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsVisible || e.Handled)
            return;

        if (e.Key != Key.V || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        // 添加区那两个框要拆「名字@服务器」，所以也接管；改角色名/艇名的输入框留给 TextBox 自己粘。
        if (e.Source is TextBox box && !ReferenceEquals(box, NewNameBox) && !ReferenceEquals(box, NewWorldBox))
            return;

        e.Handled = true;

        if (DataContext is CharacterManagerViewModel viewModel)
            viewModel.PasteCommand.Execute(null);
    }
}
