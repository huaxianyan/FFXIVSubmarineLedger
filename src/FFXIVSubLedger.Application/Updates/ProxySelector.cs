using System.Net;
using FFXIVSubLedger.Domain.Settings;

namespace FFXIVSubLedger.Application.Updates;

/// <summary>
/// 按设置决定出网方式。国内直连 GitHub 常常不通，所以三种模式都要能选，
/// 并且**选错时要能说清楚错在哪**（代理地址写错了不该表现成「检查更新失败」）。
/// </summary>
public static class ProxySelector
{
    /// <summary>把自定义代理地址解析成 <see cref="IWebProxy" />。</summary>
    public static bool TryParse(string? url, out IWebProxy? proxy)
    {
        proxy = null;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        proxy = new WebProxy(uri);
        return true;
    }

    /// <summary>按设置建 <see cref="HttpClientHandler" />。</summary>
    public static HttpClientHandler CreateHandler(ProxyMode mode, string? customUrl)
    {
        if (mode != ProxyMode.Custom)
        {
            // System 用系统代理，Direct 明确关掉。
            return new HttpClientHandler { UseProxy = mode == ProxyMode.System };
        }

        var handler = new HttpClientHandler { UseProxy = true };

        if (TryParse(customUrl, out IWebProxy? proxy))
            handler.Proxy = proxy;

        return handler;
    }
}
