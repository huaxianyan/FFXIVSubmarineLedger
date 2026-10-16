using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Domain.Settings;

namespace FFXIVSubLedger.Application.Settings;

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

    public async Task SetLastSelectedCharacterAsync(Guid? characterId, CancellationToken ct = default)
    {
        AppSettings settings = await GetAsync(ct).ConfigureAwait(false);
        settings.LastSelectedCharacterId = characterId;
        await SaveAsync(settings, ct).ConfigureAwait(false);
    }
}
