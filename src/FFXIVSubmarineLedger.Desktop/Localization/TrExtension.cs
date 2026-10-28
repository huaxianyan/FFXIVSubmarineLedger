using Avalonia.Data;
using Avalonia.Markup.Xaml;
using FFXIVSubmarineLedger.Application.Localization;

namespace FFXIVSubmarineLedger.Desktop.Localization;

/// <summary>
/// XAML 里取界面文案：<c>Text="{loc:Tr Records.Save}"</c>。
///
/// 每个绑定拿到自己的 <see cref="LocalizedText" />，换语言时由 <see cref="Loc" /> 逐个通知，
/// 界面即时刷新，不用重建窗口。
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
        Path = nameof(LocalizedText.Value),
        Source = new LocalizedText(Key),
        Mode = BindingMode.OneWay,
    };
}
