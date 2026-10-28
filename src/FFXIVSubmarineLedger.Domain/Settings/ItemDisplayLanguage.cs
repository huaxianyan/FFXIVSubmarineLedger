namespace FFXIVSubmarineLedger.Domain.Settings;

/// <summary>
/// 道具名的当前显示语言，用语言标签表示（如 <c>zh-Hans</c>、<c>ja</c>）。
///
/// 单独抽出来是因为物名要显示在很多地方（确认行、候选下拉、统计明细、CSV），
/// 逐处传语言会把参数从界面一路穿到领域对象里。界面切换语言时只改这一处。
/// **只影响显示**，不参与匹配与计价。
/// </summary>
public static class ItemDisplayLanguage
{
    /// <summary>语言标签，与语言资源文件名 <c>strings.&lt;tag&gt;.json</c> 一致。</summary>
    public const string Fallback = "zh-Hans";

    public static string Current { get; set; } = Fallback;
}
