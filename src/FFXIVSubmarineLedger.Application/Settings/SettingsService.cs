using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Domain.Settings;

namespace FFXIVSubmarineLedger.Application.Settings;

/// <summary>设置的读写。整体以 JSON 存在数据库里，进程内做一次缓存。</summary>
public sealed class SettingsService(ISettingsStore store)
{
    private AppSettings? _cached;

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
        => _cached ??= await store.LoadAsync(ct).ConfigureAwait(false);

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        await store.SaveAsync(settings, ct).ConfigureAwait(false);
        _cached = settings;
    }

    public async Task SetDefaultCharacterAsync(Guid? characterId, CancellationToken ct = default)
    {
        AppSettings settings = await GetAsync(ct).ConfigureAwait(false);
        settings.DefaultCharacterId = characterId;
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }

    /// <summary>切换界面语言并落库。</summary>
    public async Task SetUiLanguageAsync(string language, CancellationToken ct = default)
    {
        AppSettings settings = await GetAsync(ct).ConfigureAwait(false);
        settings.UiLanguage = language;
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }

    /// <summary>更新检查与代理的设置一起存。</summary>
    public async Task SetUpdateOptionsAsync(
        bool checkForUpdates,
        ProxyMode proxyMode,
        ProxyProtocol proxyProtocol,
        string proxyHost,
        int proxyPort,
        CancellationToken ct = default)
    {
        AppSettings settings = await GetAsync(ct).ConfigureAwait(false);
        settings.CheckForUpdates = checkForUpdates;
        settings.ProxyMode = proxyMode;
        settings.ProxyProtocol = proxyProtocol;
        settings.ProxyHost = proxyHost ?? string.Empty;
        settings.ProxyPort = proxyPort;
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }

    public async Task SetLastSelectedCharacterAsync(Guid? characterId, CancellationToken ct = default)
    {
        AppSettings settings = await GetAsync(ct).ConfigureAwait(false);
        settings.LastSelectedCharacterId = characterId;
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }

    /// <summary>调试模式：是否把读到的聊天原文记到 txt。</summary>
    public async Task SetDumpChatLogAsync(bool dump, CancellationToken ct = default)
    {
        AppSettings settings = await GetAsync(ct).ConfigureAwait(false);
        settings.DumpChatLog = dump;
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }
}
