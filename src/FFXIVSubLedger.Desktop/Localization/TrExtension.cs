using Avalonia.Data;
using FFXIVSubLedger.Application.Localization;
using Avalonia.Markup.Xaml;

namespace FFXIVSubLedger.Desktop.Localization;

/// <summary>
/// XAML 里取界面文案：<c>Text="{loc:Tr Records.Save}"</c>。
///
/// 返回的是指向 <see cref="Loc"/> 索引器的绑定，所以换语言时界面会自己刷新，不用重建窗口。
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding
    {
        Path = $"[{Key}]",
        Source = Loc.Instance,
        Mode = BindingMode.OneWay,
    };
}
