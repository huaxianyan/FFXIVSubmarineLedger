using System.Text.Json;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Updates;
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
        return Task.FromResult(MigrateLegacyProxyUrl(settings, json));
    }

    /// <summary>
    /// 旧版把代理存成一整串 URL（<c>ProxyUrl</c>），现在拆成协议、主机、端口三个字段。
    /// 不把旧值搬过来的话，用户升级后代理会静默失效——他只会看到「检查更新失败」。
    /// </summary>
    private static AppSettings MigrateLegacyProxyUrl(AppSettings settings, string json)
    {
        if (settings.ProxyHost.Length > 0)
            return settings;

        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("ProxyUrl", out JsonElement legacy))
            return settings;

        if (legacy.ValueKind != JsonValueKind.String
            || legacy.GetString() is not { Length: > 0 } url
            || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return settings;
        }

        settings.ProxyProtocol = uri.Scheme.ToLowerInvariant() switch
        {
            "socks5" => ProxyProtocol.Socks5,
            "socks4" => ProxyProtocol.Socks4,
            "socks4a" => ProxyProtocol.Socks4a,
            _ => ProxyProtocol.Http,
        };

        settings.ProxyHost = uri.Host;
        settings.ProxyPort = uri.IsDefaultPort ? ProxySelector.DefaultPort(settings.ProxyProtocol) : uri.Port;
        return settings;
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
