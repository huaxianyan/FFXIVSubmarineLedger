using System.Net;
using FFXIVSubLedger.Application.Updates;
using FFXIVSubLedger.Domain.Settings;

namespace FFXIVSubLedger.Domain.Tests.Updates;

public sealed class UpdateCheckerTests
{
    private const string ReleaseJson = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/huaxianyan/FFXIVSubmarineLedger/releases/tag/v0.2.0",
          "published_at": "2026-11-01T09:00:00Z",
          "body": "新增功能"
        }
        """;

    [Fact]
    public async Task CheckAsync_ReportsANewerRelease()
    {
        UpdateChecker checker = Checker(HttpStatusCode.OK, ReleaseJson);

        UpdateCheckResult result = await checker.CheckAsync(new Version(0, 1, 0), ProxyMode.System, null);

        Assert.Equal(UpdateStatus.Available, result.Status);
        Assert.Equal("0.2.0", result.Release!.Version);
        Assert.Equal("https://github.com/huaxianyan/FFXIVSubmarineLedger/releases/tag/v0.2.0", result.Release.HtmlUrl);
    }

    [Fact]
    public async Task CheckAsync_SaysUpToDateWhenTheTagMatches()
    {
        UpdateChecker checker = Checker(HttpStatusCode.OK, ReleaseJson);

        UpdateCheckResult result = await checker.CheckAsync(new Version(0, 2, 0), ProxyMode.System, null);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Null(result.Release);
    }

    [Fact]
    public async Task CheckAsync_ReportsTheStatusCodeWhenGithubRefuses()
    {
        UpdateChecker checker = Checker(HttpStatusCode.Forbidden, """{"message":"rate limited"}""");

        UpdateCheckResult result = await checker.CheckAsync(new Version(0, 1, 0), ProxyMode.System, null);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Equal(UpdateFailure.HttpStatus, result.Failure);
        Assert.Equal(403, result.HttpStatus);
    }

    [Fact]
    public async Task CheckAsync_FailsQuietlyWhenTheNetworkIsDown()
    {
        UpdateChecker checker = new(
            "huaxianyan/FFXIVSubmarineLedger",
            (_, _) => new HttpClient(new StubHandler(static _ => throw new HttpRequestException("连不上"))));

        UpdateCheckResult result = await checker.CheckAsync(new Version(0, 1, 0), ProxyMode.Custom, "http://127.0.0.1:7890");

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Equal(UpdateFailure.Network, result.Failure);
        Assert.Equal("连不上", result.Detail);
    }

    [Fact]
    public async Task CheckAsync_TreatsMissingReleasesAsUpToDate()
    {
        // 仓库还没发过正式版时 GitHub 回 404：对用户来说就是「没有新版本」。
        UpdateChecker checker = Checker(HttpStatusCode.NotFound, """{"message":"Not Found"}""");

        UpdateCheckResult result = await checker.CheckAsync(new Version(0, 1, 0), ProxyMode.System, null);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Parse_RejectsResponsesWithoutAVersion()
    {
        Assert.Null(UpdateChecker.Parse("""{"html_url":"https://example.invalid"}"""));
        Assert.Null(UpdateChecker.Parse("""{"tag_name":"nightly","html_url":"https://example.invalid"}"""));
        Assert.Null(UpdateChecker.Parse("""{"tag_name":"v0.2.0"}"""));
        Assert.Null(UpdateChecker.Parse("""[]"""));
    }

    private static UpdateChecker Checker(HttpStatusCode status, string body) => new(
        "huaxianyan/FFXIVSubmarineLedger",
        (_, _) => new HttpClient(new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body),
        })));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
