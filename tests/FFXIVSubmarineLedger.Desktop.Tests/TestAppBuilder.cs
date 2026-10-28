using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(FFXIVSubmarineLedger.Desktop.Tests.TestAppBuilder))]

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>无头测试用的最小应用。故意不用真实 App：那会去开数据库、读设置。</summary>
public sealed class TestApplication : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
