using FFXIVSubmarineLedger.Application.Updates;
using System.Reflection;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Backup;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Settings;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using FFXIVSubmarineLedger.Domain.Settings;
using FFXIVSubmarineLedger.Infrastructure.Persistence;
using FFXIVSubmarineLedger.Platform.Windows;

namespace FFXIVSubmarineLedger.Desktop;

/// <summary>组合根：手工装配依赖，不引入 DI 容器。</summary>
public sealed class ApplicationComposition
{
    /// <summary>更新检查的仓库（GitHub API 路径用）。</summary>
    private const string RepositorySlug = "huaxianyan/FFXIVSubmarineLedger";

    private ApplicationComposition(SqliteDatabase database, string dataDirectory)
    {
        Database = database;
        DataDirectory = dataDirectory;

        ICharacterRepository characterRepository = new SqliteCharacterRepository(database);
        ISettingsStore settingsStore = new SqliteSettingsStore(database);

        Characters = new CharacterService(characterRepository);
        Settings = new SettingsService(settingsStore);
        Records = new SqliteLootRecordRepository(database);
        Submarines = new SqliteSubmarineRepository(database);
        Backup = new BackupService(new SqliteBackupStore(database));

        Version currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 1, 0);
        Updates = new UpdateService(
            new UpdateChecker(
                RepositorySlug,
                (mode, url) => new HttpClient(ProxySelector.CreateHandler(mode, url))),
            Settings,
            currentVersion);

        // 界面语言在开窗之前就要定下来，否则首屏会闪一下默认语言。
        AppSettings settings = Settings.GetAsync().GetAwaiter().GetResult();
        Loc.Instance.SetLanguage(settings.UiLanguage);

        // 词表只在真要用时读，读不到也不至于开不了程序。
        CatalogSource = new Lazy<ItemCatalog>(() => ItemCatalog.Load());
    }

    public Lazy<ItemCatalog> CatalogSource { get; }

    public SqliteDatabase Database { get; }

    public string DataDirectory { get; }

    public CharacterService Characters { get; }

    public SettingsService Settings { get; }

    public ILootRecordRepository Records { get; }

    public ISubmarineRepository Submarines { get; }

    /// <summary>当前的程序版本号，界面与用户代理里都用它。</summary>
    public UpdateService Updates { get; }

    public BackupService Backup { get; }

    public ItemCatalog Catalog => CatalogSource.Value;

    public static ApplicationComposition Create()
    {
        // 程序改过名：旧目录里的账要跟着搬到新目录，否则用户会以为数据丢了。
        WindowsPaths.MigrateLegacyDataDirectory();

        string dataDirectory = WindowsPaths.DefaultDataDirectory();
        SqliteDatabase database = new(WindowsPaths.DatabasePath(dataDirectory));
        return new ApplicationComposition(database, dataDirectory);
    }
}
