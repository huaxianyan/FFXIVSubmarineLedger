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

    /// <summary>
    /// 一次写入多笔，**要么全进去要么一笔都不进**。
    /// 一次收艇按「角色 + 艇」会拆成好几笔，逐笔提交时中途失败会留下半批，
    /// 用户看着「写账失败」却已经有几笔进了账，之后对不上的时候最难查。
    /// </summary>
    Task AddRangeAsync(IReadOnlyList<LootRecord> records, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
