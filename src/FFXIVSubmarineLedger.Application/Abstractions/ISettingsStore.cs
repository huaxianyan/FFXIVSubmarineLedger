using FFXIVSubmarineLedger.Domain.Settings;

namespace FFXIVSubmarineLedger.Application.Abstractions;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
