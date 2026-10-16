namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>从格位切图标时的参数。</summary>
public sealed record IconExtractionOptions
{
    /// <summary>归一化后的边长。所有图标都缩到这个尺寸再比对，尺寸差异就被抹平了。</summary>
    public int PatchSize { get; init; } = 32;

    /// <summary>
    /// 向内收缩的比例，用来把格子边框和圆角排除掉。
    /// 边框在各处宽度一致但颜色随底色变化，留着会拉低同图标的相似度。
    /// </summary>
    public double InsetRatio { get; init; } = 0.08;

    /// <summary>
    /// 数量数字区域的左边界，相对整个格子的比例。右侧超过这个比例才算数字区。
    /// 取 0.45 是为了盖住三位数——实测「111」比「27」宽出一截，
    /// 只按两位数的宽度屏蔽会让首位数字漏在比对区域里。
    /// </summary>
    public double QuantityLeft { get; init; } = 0.45;

    /// <summary>数量数字区域的上边界，相对整个格子的比例。下方超过这个比例才算数字区。</summary>
    public double QuantityTop { get; init; } = 0.60;
}
