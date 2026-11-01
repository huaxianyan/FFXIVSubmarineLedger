using FFXIVSubmarineLedger.Application.Settings;
using FFXIVSubmarineLedger.Domain.Settings;

namespace FFXIVSubmarineLedger.Application.Updates;

/// <summary>
/// 更新检查的门面：读设置里的代理、只查一次、留住上次结果。
///
/// 启动时自动查一次，设置页手动查也走这里，两边拿到的是同一份结果。
/// </summary>
public sealed class UpdateService(UpdateChecker checker, SettingsService settings, Version current)
{
    /// <summary>上次检查的结果。没查过时为 null。</summary>
    public UpdateCheckResult? Last { get; private set; }

    public Version Current { get; } = current;

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        AppSettings options = await settings.GetAsync(ct).ConfigureAwait(false);

        Last = await checker
            .CheckAsync(
                Current,
                options.ProxyMode,
                options.ProxyProtocol,
                options.ProxyHost,
                options.ProxyPort,
                ct)
            .ConfigureAwait(false);

        return Last;
    }
}
