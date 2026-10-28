namespace FFXIVSubmarineLedger.Application.Updates;

/// <summary>GitHub Release 里用得上的字段。</summary>
/// <param name="Version">去掉前缀 <c>v</c> 的版本号，如 <c>0.2.0</c>。</param>
/// <param name="TagName">原始 tag，如 <c>v0.2.0</c>。</param>
/// <param name="HtmlUrl">发布页地址，用户点「打开发布页」就到这里。</param>
/// <param name="PublishedAt">发布时间，没有就为 null。</param>
public sealed record ReleaseInfo(string Version, string TagName, string HtmlUrl, DateTimeOffset? PublishedAt);

public enum UpdateStatus
{
    /// <summary>已经是最新的（包括「还没发布过任何版本」）。</summary>
    UpToDate,

    /// <summary>有更新的版本。</summary>
    Available,

    /// <summary>检查没成功，具体原因看 <see cref="UpdateCheckResult.Failure" />。</summary>
    Failed,
}

/// <summary>检查失败的原因。界面按它挑文案，不直接展示原始异常。</summary>
public enum UpdateFailure
{
    /// <summary>没失败。</summary>
    None,

    /// <summary>连不上：断网、代理不通、超时。</summary>
    Network,

    /// <summary>GitHub 回了非 2xx。</summary>
    HttpStatus,

    /// <summary>回来了但读不懂（没有版本号之类）。</summary>
    BadResponse,
}

/// <summary>一次更新检查的结果。</summary>
public sealed record UpdateCheckResult(
    UpdateStatus Status,
    Version Current,
    ReleaseInfo? Release = null,
    UpdateFailure Failure = UpdateFailure.None,
    int? HttpStatus = null,
    string? Detail = null)
{
    public static UpdateCheckResult UpToDate(Version current) => new(UpdateStatus.UpToDate, current);

    public static UpdateCheckResult Available(Version current, ReleaseInfo release) =>
        new(UpdateStatus.Available, current, release);

    public static UpdateCheckResult NetworkFailed(Version current, string detail) =>
        new(UpdateStatus.Failed, current, Failure: UpdateFailure.Network, Detail: detail);

    public static UpdateCheckResult HttpFailed(Version current, int status) =>
        new(UpdateStatus.Failed, current, Failure: UpdateFailure.HttpStatus, HttpStatus: status);

    public static UpdateCheckResult BadResponse(Version current) =>
        new(UpdateStatus.Failed, current, Failure: UpdateFailure.BadResponse);
}
