using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Settings;
using FFXIVSubLedger.Infrastructure.Persistence;
using FFXIVSubLedger.Platform.Windows;

namespace FFXIVSubLedger.Desktop;

/// <summary>组合根：手工装配依赖，不引入 DI 容器。</summary>
public sealed class ApplicationComposition
{
    private ApplicationComposition(SqliteDatabase database)
    {
        Database = database;

        ICharacterRepository characterRepository = new SqliteCharacterRepository(database);
        ISettingsStore settingsStore = new SqliteSettingsStore(database);

        Characters = new CharacterService(characterRepository);
        Settings = new SettingsService(settingsStore);
        Records = new SqliteLootRecordRepository(database);
    }

    public SqliteDatabase Database { get; }

    public CharacterService Characters { get; }

    public SettingsService Settings { get; }

    public ILootRecordRepository Records { get; }

    public static ApplicationComposition Create()
    {
        string dataDirectory = WindowsPaths.DefaultDataDirectory();
        SqliteDatabase database = new(WindowsPaths.DatabasePath(dataDirectory));
        return new ApplicationComposition(database);
    }
}
