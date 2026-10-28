using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Application.Abstractions;

public interface ILootRecordRepository
{
    Task<IReadOnlyList<LootRecord>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? characterId = null,
        CancellationToken ct = default);

    Task<LootRecord?> FindAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(LootRecord record, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
