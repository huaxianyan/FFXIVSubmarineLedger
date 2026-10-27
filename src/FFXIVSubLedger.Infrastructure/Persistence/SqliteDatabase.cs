using Microsoft.Data.Sqlite;

namespace FFXIVSubLedger.Infrastructure.Persistence;

/// <summary>SQLite 连接与建表。数据库是单个文件，随程序便携。</summary>
public sealed class SqliteDatabase
{
    private readonly string _connectionString;

    public SqliteDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public SqliteConnection Open()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        string? directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Schema;
        command.ExecuteNonQuery();
    }

    private const string Schema =
        """
        CREATE TABLE IF NOT EXISTS characters (
            id          TEXT    PRIMARY KEY,
            name        TEXT    NOT NULL,
            world       TEXT    NOT NULL,
            is_active   INTEGER NOT NULL DEFAULT 1,
            is_default  INTEGER NOT NULL DEFAULT 0,
            created_at  TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS free_companies (
            id            TEXT PRIMARY KEY,
            name          TEXT NOT NULL,
            character_id  TEXT NOT NULL,
            created_at    TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS loot_records (
            id               TEXT PRIMARY KEY,
            character_id     TEXT NOT NULL,
            free_company_id  TEXT NULL,
            date             TEXT NOT NULL,
            created_at       TEXT NOT NULL,
            note             TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS loot_items (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            record_id   TEXT    NOT NULL,
            item_id     INTEGER NOT NULL,
            item_name   TEXT    NOT NULL,
            quantity    INTEGER NOT NULL,
            is_high_quality INTEGER NOT NULL DEFAULT 0,
            unit_price  INTEGER NOT NULL,
            is_unknown  INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (record_id) REFERENCES loot_records (id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS settings (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_loot_records_date      ON loot_records (date);
        CREATE INDEX IF NOT EXISTS idx_loot_records_character ON loot_records (character_id);
        CREATE INDEX IF NOT EXISTS idx_loot_items_record      ON loot_items (record_id);
        """;
}
