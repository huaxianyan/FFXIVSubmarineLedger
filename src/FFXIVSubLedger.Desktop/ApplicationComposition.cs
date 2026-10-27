using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Backup;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Settings;
using FFXIVSubLedger.Infrastructure.Catalog;
using FFXIVSubLedger.Infrastructure.Persistence;
using FFXIVSubLedger.Platform.Windows;

namespace FFXIVSubLedger.Desktop;

/// <summary>组合根：手工装配依赖，不引入 DI 容器。</summary>
public sealed class ApplicationComposition
{
    private ApplicationComposition(SqliteDatabase database, string dataDirectory)
    {
        Database = database;
        DataDirectory = dataDirectory;

        ICharacterRepository characterRepository = new SqliteCharacterRepository(database);
        ISettingsStore settingsStore = new SqliteSettingsStore(database);

        Characters = new CharacterService(characterRepository);
        Settings = new SettingsService(settingsStore);
        Records = new SqliteLootRecordRepository(database);
        Backup = new BackupService(new SqliteBackupStore(database));

        // 词表只在真要用时读，读不到也不至于开不了程序。
        CatalogSource = new Lazy<ItemCatalog>(() => ItemCatalog.Load());
    }

    public Lazy<ItemCatalog> CatalogSource { get; }

    public SqliteDatabase Database { get; }

    public string DataDirectory { get; }

    public CharacterService Characters { get; }

    public SettingsService Settings { get; }

    public ILootRecordRepository Records { get; }

    public BackupService Backup { get; }

    public ItemCatalog Catalog => CatalogSource.Value;

    public static ApplicationComposition Create()
    {
        string dataDirectory = WindowsPaths.DefaultDataDirectory();
        SqliteDatabase database = new(WindowsPaths.DatabasePath(dataDirectory));
        return new ApplicationComposition(database, dataDirectory);
    }
}
