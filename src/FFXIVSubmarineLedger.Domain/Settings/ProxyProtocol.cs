namespace FFXIVSubmarineLedger.Domain.Settings;

/// <summary>
/// 代理的协议。只列 HttpClient 真能吃下的几种——<c>SocketsHttpHandler</c> 只认
/// http / socks4 / socks4a / socks5，写 <c>https://</c> 会在运行时抛
/// <see cref="NotSupportedException" />，所以界面上干脆不提供它。
/// </summary>
public enum ProxyProtocol
{
    Http = 0,
    Socks5 = 1,
    Socks4 = 2,
    Socks4a = 3,
}
