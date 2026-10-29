using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Settings;
using FFXIVSubmarineLedger.Desktop.Localization;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 界面文案的绑定必须能在运行中换语言时刷新——用户反馈过「切到日语还有很多中文」，
/// 就是这条路径没验证过：之前只验证了「用日语启动」。
/// </summary>
[Collection(LocalizationCollection.Name)]
public sealed class LocalizationBindingTests
{
    [AvaloniaFact]
    public void TextBoundThroughTrFollowsLanguageSwitch()
    {
        var block = new TextBlock();
        block.Bind(TextBlock.TextProperty, (BindingBase)new TrExtension("nav.settings").ProvideValue(null!));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("设置", block.Text);

        Loc.Instance.SetLanguage("ja");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("設定", block.Text);

        Loc.Instance.SetLanguage(ItemDisplayLanguage.Fallback);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("设置", block.Text);
    }
}
