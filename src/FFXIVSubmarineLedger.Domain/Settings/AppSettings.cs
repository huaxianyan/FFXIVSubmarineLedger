namespace FFXIVSubmarineLedger.Domain.Settings;

/// <summary>应用级设置。以 JSON 形式整体存在数据库的 settings 表里。</summary>
public sealed class AppSettings
{
    /// <summary>多角色时的默认归属角色。</summary>
    public Guid? DefaultCharacterId { get; set; }

    /// <summary>上次选择的角色，用于预选。</summary>
    public Guid? LastSelectedCharacterId { get; set; }

    /// <summary>
    /// 界面与道具名的显示语言，用语言标签（如 <c>zh-Hans</c>、<c>ja</c>）。
    /// 不影响收集：日志用什么语言由解析器决定。
    /// </summary>
    public string UiLanguage { get; set; } = ItemDisplayLanguage.Fallback;

    /// <summary>启动时检查有没有新版本。关掉之后只能手动点检查。</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>更新检查的出网方式。</summary>
    public ProxyMode ProxyMode { get; set; } = ProxyMode.System;

    /// <summary><see cref="ProxyMode.Custom" /> 时用的协议、主机与端口，分开存，界面上也是分开填。</summary>
    public ProxyProtocol ProxyProtocol { get; set; } = ProxyProtocol.Http;

    /// <summary>代理主机，如 <c>127.0.0.1</c>。不带协议、不带端口。</summary>
    public string ProxyHost { get; set; } = string.Empty;

    /// <summary>代理端口，如 <c>7890</c>。</summary>
    public int ProxyPort { get; set; }

    /// <summary>格子边长小于该像素值时，数量交给用户手输，不做自动识别。</summary>
    public int MinCellSizeForQuantityOcr { get; set; } = 40;

    /// <summary>
    /// 调试模式：把从游戏聊天框读到的原文记到 txt。
    /// 用于其它语言的客户端用户把样本发回来，照着补对应语言的解析规则。
    /// </summary>
    public bool DumpChatLog { get; set; }
}
