using FFXIVSubLedger.Application.Updates;
using FFXIVSubLedger.Domain.Settings;

namespace FFXIVSubLedger.Domain.Tests.Updates;

public sealed class ProxySelectorTests
{
    [Theory]
    [InlineData("http://127.0.0.1:7890")]
    [InlineData("https://proxy.example.com:8443")]
    public void TryParse_AcceptsHttpAddresses(string url)
    {
        Assert.True(ProxySelector.TryParse(url, out var proxy));
        Assert.NotNull(proxy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("127.0.0.1:7890")]
    [InlineData("socks5://127.0.0.1:7890")]
    [InlineData("not a url")]
    public void TryParse_RejectsEverythingElse(string url) =>
        Assert.False(ProxySelector.TryParse(url, out _));

    [Theory]
    [InlineData(ProxyMode.System, true)]
    [InlineData(ProxyMode.Direct, false)]
    public void CreateHandler_FollowsTheMode(ProxyMode mode, bool useProxy)
    {
        using var handler = ProxySelector.CreateHandler(mode, null);

        Assert.Equal(useProxy, handler.UseProxy);
    }

    [Fact]
    public void CreateHandler_UsesTheCustomAddress()
    {
        using var handler = ProxySelector.CreateHandler(ProxyMode.Custom, "http://127.0.0.1:7890");

        Assert.True(handler.UseProxy);
        Assert.NotNull(handler.Proxy);

        var address = handler.Proxy!.GetProxy(new Uri("https://api.github.com"));
        Assert.Equal(new Uri("http://127.0.0.1:7890"), address);
    }
}
