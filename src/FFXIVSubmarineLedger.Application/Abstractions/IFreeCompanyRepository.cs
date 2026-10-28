using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Application.Abstractions;

public interface IFreeCompanyRepository
{
    Task<IReadOnlyList<FreeCompany>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<FreeCompany>> ListByCharacterAsync(Guid characterId, CancellationToken ct = default);

    Task AddAsync(FreeCompany freeCompany, CancellationToken ct = default);

    Task UpdateAsync(FreeCompany freeCompany, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
