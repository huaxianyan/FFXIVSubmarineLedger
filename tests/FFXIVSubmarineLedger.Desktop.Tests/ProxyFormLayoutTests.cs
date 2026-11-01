using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 自定义代理的表单：协议下拉 + 地址框 + 端口框，三项各自独立可见。
/// 用户不需要自己写 <c>http://</c>、冒号或斜杠。
/// </summary>
public sealed class ProxyFormLayoutTests
{
    [AvaloniaFact]
    public void CustomModeShowsProtocolHostAndPortSeparately()
    {
        (Window window, SettingsPageViewModel settings) = Show();

        settings.SelectedProxy = settings.ProxyOptions.First(x => x.Mode == Domain.Settings.ProxyMode.Custom);
        Dispatcher.UIThread.RunJobs();

        List<ComboBox> visibleCombo = [.. Visible<ComboBox>(window)];
        List<TextBox> visibleBoxes = [.. Visible<TextBox>(window)];

        // 协议下拉：和代理模式那个下拉并列的第二个
        Assert.True(visibleCombo.Count >= 2, $"只看到 {visibleCombo.Count} 个下拉");

        // 地址与端口各一个输入框
        Assert.True(visibleBoxes.Count >= 2, $"只看到 {visibleBoxes.Count} 个输入框");

        Assert.Contains(visibleBoxes, x => x.PlaceholderText == "127.0.0.1");
        Assert.Contains(visibleBoxes, x => x.PlaceholderText == "7890");

        window.Close();
    }

    [AvaloniaFact]
    public void SwitchingProtocolFillsInThatProtocolsDefaultPort()
    {
        (Window window, SettingsPageViewModel settings) = Show();

        settings.SelectedProxy = settings.ProxyOptions.First(x => x.Mode == Domain.Settings.ProxyMode.Custom);
        settings.SelectedProtocol = settings.ProtocolOptions.First(x => x.Protocol == Domain.Settings.ProxyProtocol.Http);

        settings.SelectedProtocol = settings.ProtocolOptions.First(x => x.Protocol == Domain.Settings.ProxyProtocol.Socks5);
        Assert.Equal("1080", settings.ProxyPort);

        window.Close();
    }

    [AvaloniaFact]
    public void HostWithASchemeIsRejectedWithAnExplanation()
    {
        (Window window, SettingsPageViewModel settings) = Show();

        settings.SelectedProxy = settings.ProxyOptions.First(x => x.Mode == Domain.Settings.ProxyMode.Custom);
        settings.ProxyHost = "http://127.0.0.1";
        settings.ProxyPort = "7890";
        settings.SaveUpdateOptions();

        Assert.Equal("地址里不用写 http:// 这类前缀，协议在上面选。", settings.StatusMessage);

        window.Close();
    }

    /// <summary>
    /// 三项排在同一行、从左到右依次是协议、地址、端口，互不重叠，也不会被窗口切掉。
    /// 这是「填写方便」的实际含义：用户不用在一个框里拼 URL。
    /// </summary>
    [AvaloniaFact]
    public void TheThreeFieldsSitInOneRowInReadingOrder()
    {
        (Window window, SettingsPageViewModel settings) = Show();

        settings.SelectedProxy = settings.ProxyOptions.First(x => x.Mode == Domain.Settings.ProxyMode.Custom);
        Dispatcher.UIThread.RunJobs();

        // 协议下拉的选项类型是 ProtocolOption，按这个认，别按位置猜。
        ComboBox protocol = Visible<ComboBox>(window).First(x =>
            x.ItemsSource is IEnumerable<ProtocolOption>);
        TextBox host = Visible<TextBox>(window).First(x => x.PlaceholderText == "127.0.0.1");
        TextBox port = Visible<TextBox>(window).First(x => x.PlaceholderText == "7890");

        Rect protocolBox = BoxIn(window, protocol);
        Rect hostBox = BoxIn(window, host);
        Rect portBox = BoxIn(window, port);

        // 同一行：竖直中心线相差不到一个控件高度。
        Assert.InRange(Math.Abs(protocolBox.Center.Y - hostBox.Center.Y), 0, protocolBox.Height);
        Assert.InRange(Math.Abs(hostBox.Center.Y - portBox.Center.Y), 0, hostBox.Height);

        // 从左到右，且不重叠。
        Assert.True(protocolBox.Right <= hostBox.Left + 1, "协议和地址重叠了");
        Assert.True(hostBox.Right <= portBox.Left + 1, "地址和端口重叠了");

        // 都在窗口内，没被切掉。
        Assert.True(portBox.Right <= window.Width, $"端口框超出了窗口宽度 {window.Width}");

        window.Close();
    }

    private static Rect BoxIn(Window window, Control control)
    {
        Avalonia.Point origin = control.TranslatePoint(default, window) ?? default;
        return new Rect(origin, control.Bounds.Size);
    }

    private static IEnumerable<T> Visible<T>(Window window) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Where(x => x.Bounds.Width > 0 && x.Bounds.Height > 0);

    private static (Window Window, SettingsPageViewModel Settings) Show()
    {
        var settings = new Application.Settings.SettingsService(new InMemorySettings());
        var page = new SettingsPageViewModel(
            () => null,
            new Application.Backup.BackupService(new InMemoryBackup()),
            settings,
            new Application.Updates.UpdateService(
                new Application.Updates.UpdateChecker("owner/repo", (_, _, _, _) => new HttpClient()),
                settings,
                new Version(0, 1, 0)),
            Path.GetTempPath(),
            Path.Combine(Path.GetTempPath(), "settings-test.db"),
            new Lazy<Infrastructure.Catalog.ItemCatalog>(() => Infrastructure.Catalog.ItemCatalog.Load()));

        var window = new Window { Content = new Views.SettingsPageView { DataContext = page }, Width = 1100, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, page);
    }

    private sealed class InMemorySettings : Application.Abstractions.ISettingsStore
    {
        private Domain.Settings.AppSettings _settings = new();

        public Task<Domain.Settings.AppSettings> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(Domain.Settings.AppSettings settings, CancellationToken ct = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCharacters : Application.Abstractions.ICharacterRepository
    {
        public Task<IReadOnlyList<Domain.Characters.Character>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Domain.Characters.Character>>([]);

        public Task<Domain.Characters.Character?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Domain.Characters.Character?>(null);

        public Task AddAsync(Domain.Characters.Character character, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateAsync(Domain.Characters.Character character, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryBackup : Application.Abstractions.IBackupStore
    {
        public Task<Application.Backup.BackupDocument> ExportAsync(CancellationToken ct = default) =>
            Task.FromResult(new Application.Backup.BackupDocument(
                2, DateTimeOffset.Now, [], [], [], []));

        public Task ImportAsync(Application.Backup.BackupDocument document, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
