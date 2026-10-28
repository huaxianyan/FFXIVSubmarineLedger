using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Application.Abstractions;

public interface ICharacterRepository
{
    Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default);

    Task<Character?> FindAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Character character, CancellationToken ct = default);

    Task UpdateAsync(Character character, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
