using FFXIVSubmarineLedger.Application.Updates;
using FFXIVSubmarineLedger.Domain.Settings;

namespace FFXIVSubmarineLedger.Domain.Tests.Updates;

/// <summary>
/// 自定义代理是分开填协议、地址、端口的，用户不用写 <c>http://</c> 也不用写冒号。
/// 这些测试钉住两件事：拼出来的地址正确，误填时给出的是「该怎么填」而不是笼统的「无效」。
/// </summary>
public sealed class ProxySelectorTests
{
    [Theory]
    [InlineData(ProxyMode.System, true)]
    [InlineData(ProxyMode.Direct, false)]
    public void CreateHandler_FollowsTheMode(ProxyMode mode, bool useProxy)
    {
        using var handler = ProxySelector.CreateHandler(mode, ProxyProtocol.Http, null, 0);

        Assert.Equal(useProxy, handler.UseProxy);
    }

    [Theory]
    [InlineData(ProxyProtocol.Http, 7890, "http://127.0.0.1:7890")]
    [InlineData(ProxyProtocol.Socks5, 7891, "socks5://127.0.0.1:7891")]
    [InlineData(ProxyProtocol.Socks4, 1080, "socks4://127.0.0.1:1080")]
    [InlineData(ProxyProtocol.Socks4a, 1081, "socks4a://127.0.0.1:1081")]
    public void CreateHandler_BuildsTheAddressFromTheThreeFields(
        ProxyProtocol protocol, int port, string expected)
    {
        using var handler = ProxySelector.CreateHandler(ProxyMode.Custom, protocol, "127.0.0.1", port);

        Assert.True(handler.UseProxy);
        Assert.NotNull(handler.Proxy);
        Assert.Equal(new Uri(expected), handler.Proxy!.GetProxy(new Uri("https://api.github.com")));
    }

    [Fact]
    public void CreateHandler_TrimsTheHost()
    {
        using var handler = ProxySelector.CreateHandler(ProxyMode.Custom, ProxyProtocol.Socks5, "  127.0.0.1  ", 1080);

        Assert.Equal(
            new Uri("socks5://127.0.0.1:1080"),
            handler.Proxy!.GetProxy(new Uri("https://api.github.com")));
    }

    [Theory]
    [InlineData(ProxyProtocol.Http, 8080)]
    [InlineData(ProxyProtocol.Socks5, 1080)]
    [InlineData(ProxyProtocol.Socks4, 1080)]
    [InlineData(ProxyProtocol.Socks4a, 1080)]
    public void DefaultPort_BelongsToTheProtocol(ProxyProtocol protocol, int expected) =>
        Assert.Equal(expected, ProxySelector.DefaultPort(protocol));

    /// <summary>
    /// 旧版让用户自己写 URL，写 <c>https://</c> 能存进设置、要到发请求时才炸
    /// （SocketsHttpHandler 只认 http/socks4/socks4a/socks5）。
    /// 现在协议是选的，列表里根本没有 https，这条钉住别哪天又加回来。
    /// </summary>
    [Fact]
    public void OnlyTheSchemesThatHttpClientAcceptsAreOffered()
    {
        string[] schemes = [.. Enum.GetValues<ProxyProtocol>().Select(ProxySelector.SchemeOf)];

        Assert.Equal(["http", "socks5", "socks4", "socks4a"], schemes);
        Assert.DoesNotContain("https", schemes);
    }

    [Fact]
    public void Validate_AcceptsWhatTheFormCollects()
    {
        Assert.Null(ProxySelector.Validate(ProxyProtocol.Socks5, "127.0.0.1", 7890));
        Assert.Null(ProxySelector.Validate(ProxyProtocol.Http, "proxy.example.com", 8080));
    }

    [Theory]
    [InlineData(ProxyProtocol.Http, "", 7890, "settings.proxyEmptyHost")]
    [InlineData(ProxyProtocol.Http, "   ", 7890, "settings.proxyEmptyHost")]
    [InlineData(ProxyProtocol.Http, "http://127.0.0.1", 7890, "settings.proxyNoScheme")]
    [InlineData(ProxyProtocol.Http, "127.0.0.1:7890", 7890, "settings.proxyPortInHost")]
    [InlineData(ProxyProtocol.Http, "127.0.0.1/proxy", 7890, "settings.proxyNoPath")]
    [InlineData(ProxyProtocol.Http, "127.0.0.1", 0, "settings.proxyBadPort")]
    [InlineData(ProxyProtocol.Http, "127.0.0.1", 70000, "settings.proxyBadPort")]
    public void Validate_SaysHowToFillItIn(ProxyProtocol protocol, string host, int port, string expected) =>
        Assert.Equal(expected, ProxySelector.Validate(protocol, host, port));
}
