using System.Net.Http.Headers;
using System.Text.Json;
using FFXIVSubmarineLedger.Domain.Settings;

namespace FFXIVSubmarineLedger.Application.Updates;

/// <summary>
/// 查 GitHub 上最新的正式版，跟当前版本比。
///
/// 只用公开 API、不要 token：未认证每小时 60 次，一天手动点几下完全够用。
/// 网络失败一律归为「检查失败」并把原因带回去，不抛异常——更新检查不该影响记账。
/// </summary>
public sealed class UpdateChecker(
    string repository,
    Func<ProxyMode, ProxyProtocol, string?, int, HttpClient> clientFactory)
{
    /// <summary>检查超时。慢到 10 秒还没回应，就当它是网络不通。</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<UpdateCheckResult> CheckAsync(
        Version current,
        ProxyMode proxyMode,
        ProxyProtocol proxyProtocol,
        string? proxyHost,
        int proxyPort,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(current);

        string url = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"https://api.github.com/repos/{repository}/releases/latest");

        try
        {
            using HttpClient client = clientFactory(proxyMode, proxyProtocol, proxyHost, proxyPort);
            client.Timeout = Timeout;
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FFXIVSubmarineLedger", AppVersion.Describe(current)));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // 仓库还没发过任何正式版时 GitHub 回 404。这不是错误，是「没有比当前更新的东西」。
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return UpdateCheckResult.UpToDate(current);

            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.HttpFailed(current, (int)response.StatusCode);

            ReleaseInfo? release = Parse(body);

            if (release is null)
                return UpdateCheckResult.BadResponse(current);

            return AppVersion.IsNewer(release.Version, current)
                ? UpdateCheckResult.Available(current, release)
                : UpdateCheckResult.UpToDate(current);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // 断网、代理连不上、超时都落在这里：对用户来说是同一件事——这次没查到。
            return UpdateCheckResult.NetworkFailed(current, exception.Message);
        }
    }

    /// <summary>解析 GitHub Release 响应。字段缺失或版本号读不出来时返回 null。</summary>
    public static ReleaseInfo? Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        string? tag = root.TryGetProperty("tag_name", out JsonElement tagElement) ? tagElement.GetString() : null;
        if (tag is null || !AppVersion.TryParse(tag, out Version version))
            return null;

        string? htmlUrl = root.TryGetProperty("html_url", out JsonElement urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(htmlUrl))
            return null;

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out JsonElement publishedElement)
            && publishedElement.ValueKind == JsonValueKind.String
            && publishedElement.TryGetDateTimeOffset(out DateTimeOffset parsed))
        {
            publishedAt = parsed;
        }

        return new ReleaseInfo(AppVersion.Describe(version), tag, htmlUrl, publishedAt);
    }
}
