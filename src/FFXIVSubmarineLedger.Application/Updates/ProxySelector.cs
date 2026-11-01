using System.Globalization;
using System.Net;
using FFXIVSubmarineLedger.Domain.Settings;

namespace FFXIVSubmarineLedger.Application.Updates;

/// <summary>
/// 按设置决定出网方式。国内直连 GitHub 常常不通，所以三种模式都要能选，
/// 并且**选错时要能说清楚错在哪**（代理地址写错了不该表现成「检查更新失败」）。
///
/// 自定义代理在界面上是分开填的：协议、主机、端口。用户不需要知道
/// <c>socks5://host:port</c> 这种写法，也不需要记哪个协议该配哪个默认端口。
/// </summary>
public static class ProxySelector
{
    /// <summary>
    /// 各协议的默认端口。用户没填端口时按这个补，省得他再去查。
    /// </summary>
    public static int DefaultPort(ProxyProtocol protocol) => protocol switch
    {
        ProxyProtocol.Http => 8080,
        ProxyProtocol.Socks5 => 1080,
        ProxyProtocol.Socks4 => 1080,
        ProxyProtocol.Socks4a => 1080,
        _ => 8080,
    };

    /// <summary>协议在 URI 里的写法。<c>SocketsHttpHandler</c> 只认这四个。</summary>
    public static string SchemeOf(ProxyProtocol protocol) => protocol switch
    {
        ProxyProtocol.Http => "http",
        ProxyProtocol.Socks5 => "socks5",
        ProxyProtocol.Socks4 => "socks4",
        ProxyProtocol.Socks4a => "socks4a",
        _ => "http",
    };

    /// <summary>拼成 <c>WebProxy</c> 能吃的 URI。只在内部用，不给用户看。</summary>
    public static Uri BuildUri(ProxyProtocol protocol, string host, int port) =>
        new(string.Create(
            CultureInfo.InvariantCulture,
            $"{SchemeOf(protocol)}://{host.Trim()}:{(port > 0 ? port : DefaultPort(protocol))}"));

    /// <summary>
    /// 校验用户填的协议、主机、端口。返回一个语言键（由界面翻成当前语言），合法时为 null。
    ///
    /// 特意认几种常见误填——把整条 <c>http://host:port</c> 塞进地址栏、把端口写进地址里——
    /// 然后指出来该分开放，而不是笼统说一句「地址无效」。
    /// </summary>
    public static string? Validate(ProxyProtocol protocol, string? host, int port)
    {
        if (!Enum.IsDefined(protocol))
            return "settings.proxyBadProtocol";

        if (string.IsNullOrWhiteSpace(host))
            return "settings.proxyEmptyHost";

        string trimmed = host.Trim();

        if (trimmed.Contains("://", StringComparison.Ordinal))
            return "settings.proxyNoScheme";

        if (trimmed.Contains(':', StringComparison.Ordinal))
            return "settings.proxyPortInHost";

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
            return "settings.proxyNoPath";

        if (port is < 1 or > 65535)
            return "settings.proxyBadPort";

        return null;
    }

    /// <summary>按设置建 <see cref="HttpClientHandler" />。</summary>
    public static HttpClientHandler CreateHandler(ProxyMode mode, ProxyProtocol protocol, string? host, int port)
    {
        if (mode != ProxyMode.Custom)
        {
            // System 用系统代理，Direct 明确关掉。
            return new HttpClientHandler { UseProxy = mode == ProxyMode.System };
        }

        var handler = new HttpClientHandler
        {
            UseProxy = true,
            Proxy = new WebProxy(BuildUri(protocol, host?.Trim() ?? string.Empty, port)),
        };

        return handler;
    }
}
