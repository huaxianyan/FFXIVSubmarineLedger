using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Application.Settings;
using FFXIVSubLedger.Domain.Characters;
using FFXIVSubLedger.Domain.Records;
using FFXIVSubLedger.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FFXIVSubLedger.Domain.Tests;

/// <summary>确认 SQLite 建表、读写、级联删除与设置序列化都真的能跑通。</summary>
public sealed class PersistenceSmokeTests : IDisposable
{
    private readonly string _directory;
    private readonly SqliteDatabase _database;

    public PersistenceSmokeTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "subledger-tests-" + Guid.NewGuid().ToString("N"));
        _database = new SqliteDatabase(Path.Combine(_directory, "test.db"));
        _database.Initialize();
    }

    public void Dispose()
    {
        // 连接池会继续持有数据库文件句柄，删除临时目录前必须先清空。
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task CharacterRoundTripsThroughSqlite()
    {
        CharacterService service = new(new SqliteCharacterRepository(_database));

        Character created = await service.AddAsync("Alice", "红玉海");
        IReadOnlyList<Character> all = await service.ListAsync(includeInactive: true);

        Assert.Single(all);
        Assert.Equal(created.Id, all[0].Id);
        Assert.Equal("Alice", all[0].Name);
        Assert.Equal("红玉海", all[0].World);
        Assert.True(all[0].IsDefault);
    }

    [Fact]
    public async Task SettingsRoundTripThroughSqlite()
    {
        SettingsService service = new(new SqliteSettingsStore(_database));
        Guid characterId = Guid.NewGuid();

        await service.SetDefaultCharacterAsync(characterId);

        SettingsService reloaded = new(new SqliteSettingsStore(_database));
        Assert.Equal(characterId, (await reloaded.GetAsync()).DefaultCharacterId);
    }

    [Fact]
    public async Task LootRecordStoresItemsAndComputesTotal()
    {
        SqliteCharacterRepository characters = new(_database);
        Character character = new() { Name = "Alice", World = "红玉海", IsDefault = true };
        await characters.AddAsync(character);

        SqliteLootRecordRepository records = new(_database);
        LootRecord record = new()
        {
            CharacterId = character.Id,
            Date = new DateOnly(2026, 10, 14),
            Items =
            [
                new LootItem { ItemId = 1, ItemName = "上等沉船项链", Quantity = 3, UnitPrice = 34500 },
                new LootItem { ItemId = 2, ItemName = "沉船戒指", Quantity = 2, UnitPrice = 8000 },
            ],
        };
        await records.AddAsync(record);

        IReadOnlyList<LootRecord> loaded = await records.ListAsync();
        Assert.Single(loaded);
        Assert.Equal(2, loaded[0].Items.Count);
        Assert.Equal(3 * 34500 + 2 * 8000, loaded[0].TotalValue);
    }

    [Fact]
    public async Task SavedRecordKeepsQualityAndMergedQuantities()
    {
        SqliteCharacterRepository characters = new(_database);
        Character character = new() { Name = "Alice", World = "红玉海", IsDefault = true };
        await characters.AddAsync(character);

        SqliteLootRecordRepository records = new(_database);
        LootRecord record = LootRecordFactory.Build(
            character.Id,
            new DateOnly(2026, 10, 27),
            [
                new LootEntryInput(22507, "沈没船の高級首飾り", 5, false, 34500, false),
                new LootEntryInput(22507, "沈没船の高級首飾り", 5, false, 34500, false),
                new LootEntryInput(5189, "エメラルド", 25, true, 3, false),
            ]);

        await records.AddAsync(record);

        LootRecord loaded = Assert.Single(await records.ListAsync());
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(10, loaded.Items[0].Quantity);
        Assert.True(loaded.Items[1].IsHighQuality);
        Assert.Equal(345000 + 75, loaded.TotalValue);
    }

    [Fact]
    public async Task DeletingRecordCascadesToItems()
    {
        SqliteCharacterRepository characters = new(_database);
        Character character = new() { Name = "Alice", World = "红玉海", IsDefault = true };
        await characters.AddAsync(character);

        SqliteLootRecordRepository records = new(_database);
        LootRecord record = new()
        {
            CharacterId = character.Id,
            Items = [new LootItem { ItemId = 1, ItemName = "沉船戒指", Quantity = 1, UnitPrice = 8000 }],
        };
        await records.AddAsync(record);
        await records.DeleteAsync(record.Id);

        Assert.Empty(await records.ListAsync());
    }
}
