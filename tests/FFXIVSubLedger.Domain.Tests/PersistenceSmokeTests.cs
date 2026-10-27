using FFXIVSubLedger.Application.Backup;
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
    public async Task BackupImportReplacesEverythingInOneTransaction()
    {
        SqliteCharacterRepository characters = new(_database);
        Character kept = new() { Name = "Alice", World = "红玉海", IsDefault = true };
        await characters.AddAsync(kept);

        SqliteLootRecordRepository records = new(_database);
        await records.AddAsync(LootRecordFactory.Build(
            kept.Id,
            new DateOnly(2026, 10, 27),
            [new LootEntryInput(5189, "エメラルド", 25, true, 3, false)]));

        SqliteBackupStore store = new(_database);
        BackupDocument backup = await store.ExportAsync();

        // 导出之后又加了角色与记录，导入应该把它们一起送走。
        await characters.AddAsync(new Character { Name = "Bob", World = "红玉海" });
        await records.AddAsync(LootRecordFactory.Build(
            kept.Id,
            new DateOnly(2026, 10, 28),
            [new LootEntryInput(22500, "沈没船の指輪", 1, false, 8000, false)]));

        await store.ImportAsync(backup);

        Character restored = Assert.Single(await characters.ListAsync());
        Assert.Equal("Alice", restored.Name);

        LootRecord record = Assert.Single(await records.ListAsync());
        LootItem item = Assert.Single(record.Items);
        Assert.True(item.IsHighQuality);
        Assert.Equal(75, record.TotalValue);
    }

    [Fact]
    public async Task SubmarineAndItsRecordRoundTrip()
    {
        SqliteCharacterRepository characters = new(_database);
        Character character = new() { Name = "Alice", World = "红玉海", IsDefault = true };
        await characters.AddAsync(character);

        SqliteSubmarineRepository submarines = new(_database);
        Submarine submarine = new() { CharacterId = character.Id, Name = "Strawberry" };
        await submarines.AddAsync(submarine);

        Submarine? found = await submarines.FindByNameAsync(character.Id, "Strawberry");
        Assert.Equal(submarine.Id, found?.Id);
        Assert.Single(await submarines.ListAsync(character.Id));

        SqliteLootRecordRepository records = new(_database);
        LootRecord record = LootRecordFactory.Build(
            character.Id,
            new DateOnly(2026, 10, 27),
            [new LootEntryInput(5189, "エメラルド", 25, true, 3, false)]);
        record.SubmarineId = submarine.Id;
        await records.AddAsync(record);

        LootRecord loaded = Assert.Single(await records.ListAsync());
        Assert.Equal(submarine.Id, loaded.SubmarineId);
    }

    [Fact]
    public void Initialize_AddsTheSubmarineColumnToAnOlderDatabase()
    {
        // 老版本的库建好了但没有 submarine_id 列；升级后要能自动补上，不能要求用户重建库。
        string path = Path.Combine(_directory, "legacy.db");
        SqliteConnection.ClearAllPools();

        using (var legacy = new SqliteConnection($"Data Source={path}"))
        {
            legacy.Open();
            using var command = legacy.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE loot_records (
                    id               TEXT PRIMARY KEY,
                    character_id     TEXT NOT NULL,
                    free_company_id  TEXT NULL,
                    date             TEXT NOT NULL,
                    created_at       TEXT NOT NULL,
                    note             TEXT NOT NULL DEFAULT ''
                );
                """;
            command.ExecuteNonQuery();
        }

        var upgraded = new SqliteDatabase(path);
        upgraded.Initialize();

        using SqliteConnection connection = upgraded.Open();
        using SqliteCommand check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('loot_records') WHERE name = 'submarine_id';";

        Assert.Equal(1L, check.ExecuteScalar());
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
