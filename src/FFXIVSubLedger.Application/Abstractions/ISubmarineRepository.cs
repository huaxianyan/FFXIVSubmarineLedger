using FFXIVSubLedger.Domain.Characters;

namespace FFXIVSubLedger.Application.Abstractions;

public interface ISubmarineRepository
{
    Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default);

    Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default);

    Task AddAsync(Submarine submarine, CancellationToken ct = default);
}
