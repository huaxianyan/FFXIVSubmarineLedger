using System.Text.Json;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Domain.Settings;
using Microsoft.Data.Sqlite;

namespace FFXIVSubmarineLedger.Infrastructure.Persistence;

/// <summary>设置整体序列化成一行 JSON 存储，避免每加一个设置项就要改表结构。</summary>
public sealed class SqliteSettingsStore(SqliteDatabase database) : ISettingsStore
{
    private const string SettingsKey = "app";

    public Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", SettingsKey);

        object? value = command.ExecuteScalar();
        if (value is not string json || json.Length == 0)
            return Task.FromResult(new AppSettings());

        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        return Task.FromResult(settings);
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        string json = JsonSerializer.Serialize(settings);

        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO settings (key, value) VALUES ($key, $value)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", SettingsKey);
        command.Parameters.AddWithValue("$value", json);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}
