using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>测试用的固定样本：一个角色、一艘艇、三笔记录（同一天起往前三天）。</summary>
internal static class Sample
{
    public static Character Character { get; } = new()
    {
        Id = Guid.NewGuid(),
        Name = "Tiina Branford",
        World = "Titan",
        IsDefault = true,
    };

    public static Submarine Submarine { get; } = new()
    {
        Id = Guid.NewGuid(),
        CharacterId = Character.Id,
        Name = "Blackberry",
    };

    public static IReadOnlyList<LootRecord> Records { get; } = CreateRecords();

    public static ILootRecordRepository RecordRepository => new FakeRecords();

    public static ISubmarineRepository SubmarineRepository => new FakeSubmarines();

    public static CharacterService Characters { get; } = new(new FakeCharacters());

    private static List<LootRecord> CreateRecords()
    {
        var today = new DateOnly(2026, 10, 29);
        var records = new List<LootRecord>();

        for (int i = 0; i < 3; i++)
        {
            records.Add(new LootRecord
            {
                Id = Guid.NewGuid(),
                CharacterId = Character.Id,
                SubmarineId = Submarine.Id,
                Date = today.AddDays(-i),
                Items =
                [
                    new LootItem { ItemId = 22507, ItemName = "沈没船の高級首飾り", Quantity = 5, UnitPrice = 34500 },
                    new LootItem { ItemId = 22506, ItemName = "沈没船の高級耳飾り", Quantity = 10, UnitPrice = 30000 },
                    new LootItem { ItemId = 24908, ItemName = "引き上げられた古銭", Quantity = 20, UnitPrice = 10 },
                ],
            });
        }

        return records;
    }

    private sealed class FakeRecords : ILootRecordRepository
    {
        public Task<IReadOnlyList<LootRecord>> ListAsync(
            DateOnly? from = null, DateOnly? to = null, Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult(Records);

        public Task<LootRecord?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Records.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(LootRecord record, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSubmarines : ISubmarineRepository
    {
        public Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Submarine>>([Submarine]);

        public Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default) =>
            Task.FromResult<Submarine?>(Submarine.CharacterId == characterId && Submarine.Name == name ? Submarine : null);

        public Task AddAsync(Submarine submarine, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCharacters : ICharacterRepository
    {
        public Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Character>>([Character]);

        public Task<Character?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Character?>(Character.Id == id ? Character : null);

        public Task AddAsync(Character character, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Character character, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
