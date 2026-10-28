using System.Globalization;

namespace FFXIVSubmarineLedger.Application.Updates;

/// <summary>
/// 版本号比较。GitHub 上的 tag 习惯带前缀（<c>v0.2.0</c>），也可能带预发布后缀
/// （<c>v0.2.0-beta.1</c>），这里只比较数字部分。
///
/// 预发布后缀不单独排序：GitHub 的 <c>/releases/latest</c> 本来就不返回预发布与草稿，
/// 所以到这里的都是正式版。
/// </summary>
public static class AppVersion
{
    /// <summary>从 tag 里取出可比较的版本号。<c>v0.2.0</c> → <c>0.2.0</c>。</summary>
    public static bool TryParse(string? tag, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        string text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];

        int suffix = text.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            text = text[..suffix];

        if (!Version.TryParse(text, out Version? parsed))
            return false;

        // 只给 "0.2" 这种两位写法时补齐成 0.2.0，避免比较时把 Build 当成 -1。
        version = parsed.Build >= 0 ? parsed : new Version(parsed.Major, parsed.Minor, 0);
        return true;
    }

    /// <summary>tag 是否比当前版本新。</summary>
    public static bool IsNewer(string? tag, Version current)
    {
        if (!TryParse(tag, out Version candidate))
            return false;

        return Normalize(candidate) > Normalize(current);
    }

    /// <summary>显示用：<c>0.1.0</c>。</summary>
    public static string Describe(Version version) =>
        version.Revision > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}")
            : string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}");

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
}
