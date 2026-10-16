using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Domain.Catalog;
using Microsoft.Data.Sqlite;

namespace FFXIVSubLedger.Infrastructure.Persistence;

public sealed class SqliteLootCatalogRepository(SqliteDatabase database) : ILootCatalogRepository
{
    private const string SelectColumns = "item_id, icon_id, name, shop_price, is_built_in";

    public Task<IReadOnlyList<LootCatalogEntry>> ListAsync(CancellationToken ct = default)
        => QueryAsync($"SELECT {SelectColumns} FROM loot_catalog ORDER BY name;", null, null);

    public Task<LootCatalogEntry?> FindByItemIdAsync(uint itemId, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM loot_catalog WHERE item_id = $item;";
        command.Parameters.AddWithValue("$item", itemId);
        using SqliteDataReader reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? Map(reader) : null);
    }

    public Task<IReadOnlyList<LootCatalogEntry>> ListByIconIdAsync(uint iconId, CancellationToken ct = default)
        => QueryAsync(
            $"SELECT {SelectColumns} FROM loot_catalog WHERE icon_id = $icon ORDER BY shop_price DESC;",
            null,
            iconId);

    public Task UpsertAsync(LootCatalogEntry entry, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO loot_catalog (item_id, icon_id, name, shop_price, is_built_in)
            VALUES ($item, $icon, $name, $price, $builtin)
            ON CONFLICT (item_id) DO UPDATE SET
                icon_id = excluded.icon_id,
                name = excluded.name,
                shop_price = excluded.shop_price,
                is_built_in = excluded.is_built_in;
            """;
        Bind(command, entry);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task ReplaceBuiltInAsync(IReadOnlyList<LootCatalogEntry> entries, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        // 只重建内置数据，用户手工补充的条目保留。
        using (SqliteCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM loot_catalog WHERE is_built_in = 1;";
            delete.ExecuteNonQuery();
        }

        foreach (LootCatalogEntry entry in entries)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO loot_catalog (item_id, icon_id, name, shop_price, is_built_in)
                VALUES ($item, $icon, $name, $price, 1)
                ON CONFLICT (item_id) DO UPDATE SET
                    icon_id = excluded.icon_id,
                    name = excluded.name,
                    shop_price = excluded.shop_price,
                    is_built_in = 1;
                """;
            Bind(insert, entry);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
        return Task.CompletedTask;
    }

    private Task<IReadOnlyList<LootCatalogEntry>> QueryAsync(string sql, uint? itemId, uint? iconId)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (itemId is not null)
            command.Parameters.AddWithValue("$item", itemId.Value);
        if (iconId is not null)
            command.Parameters.AddWithValue("$icon", iconId.Value);
        using SqliteDataReader reader = command.ExecuteReader();

        List<LootCatalogEntry> result = [];
        while (reader.Read())
            result.Add(Map(reader));
        return Task.FromResult<IReadOnlyList<LootCatalogEntry>>(result);
    }

    private static void Bind(SqliteCommand command, LootCatalogEntry entry)
    {
        command.Parameters.AddWithValue("$item", entry.ItemId);
        command.Parameters.AddWithValue("$icon", entry.IconId);
        command.Parameters.AddWithValue("$name", entry.Name);
        command.Parameters.AddWithValue("$price", entry.ShopPrice);
        command.Parameters.AddWithValue("$builtin", entry.IsBuiltIn ? 1 : 0);
    }

    private static LootCatalogEntry Map(SqliteDataReader reader) => new(
        (uint)reader.GetInt64(0),
        (uint)reader.GetInt64(1),
        reader.GetString(2),
        reader.GetInt32(3),
        reader.GetInt64(4) != 0);
}
