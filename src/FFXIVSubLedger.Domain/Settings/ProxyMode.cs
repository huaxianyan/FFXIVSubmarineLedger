namespace FFXIVSubLedger.Domain.Settings;

/// <summary>更新检查走哪条网络路径。国内直连 GitHub 常常不通，所以代理要能配。</summary>
public enum ProxyMode
{
    /// <summary>跟随系统代理（默认，Windows 设置里配的那个）。</summary>
    System = 0,

    /// <summary>直连，不走代理。</summary>
    Direct = 1,

    /// <summary>用下面填的地址，如 <c>http://127.0.0.1:7890</c>。</summary>
    Custom = 2,
}
