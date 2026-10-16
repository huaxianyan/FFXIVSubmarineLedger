using System.Globalization;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Domain.Records;
using Microsoft.Data.Sqlite;

namespace FFXIVSubLedger.Infrastructure.Persistence;

public sealed class SqliteLootRecordRepository(SqliteDatabase database) : ILootRecordRepository
{
    public Task<IReadOnlyList<LootRecord>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? characterId = null,
        CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();

        List<Guid> ids = [];
        using (SqliteCommand select = connection.CreateCommand())
        {
            List<string> conditions = [];
            if (from is not null)
            {
                conditions.Add("date >= $from");
                select.Parameters.AddWithValue("$from", from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            if (to is not null)
            {
                conditions.Add("date <= $to");
                select.Parameters.AddWithValue("$to", to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            if (characterId is not null)
            {
                conditions.Add("character_id = $character");
                select.Parameters.AddWithValue("$character", characterId.Value.ToString());
            }

            string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
            select.CommandText =
                "SELECT id FROM loot_records" + where + " ORDER BY date DESC, created_at DESC;";
            using SqliteDataReader reader = select.ExecuteReader();
            while (reader.Read())
                ids.Add(Guid.Parse(reader.GetString(0)));
        }

        List<LootRecord> result = [];
        foreach (Guid id in ids)
        {
            LootRecord? record = LoadRecord(connection, id);
            if (record is not null)
                result.Add(record);
        }
        return Task.FromResult<IReadOnlyList<LootRecord>>(result);
    }

    public Task<LootRecord?> FindAsync(Guid id, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        return Task.FromResult(LoadRecord(connection, id));
    }

    public Task AddAsync(LootRecord record, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO loot_records (id, character_id, free_company_id, date, created_at, note)
                VALUES ($id, $character, $company, $date, $created, $note);
                """;
            command.Parameters.AddWithValue("$id", record.Id.ToString());
            command.Parameters.AddWithValue("$character", record.CharacterId.ToString());
            command.Parameters.AddWithValue("$company", (object?)record.FreeCompanyId?.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("$date", record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$created", record.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$note", record.Note);
            command.ExecuteNonQuery();
        }

        foreach (LootItem item in record.Items)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO loot_items (record_id, item_id, item_name, quantity, unit_price, is_unknown)
                VALUES ($record, $item, $name, $quantity, $price, $unknown);
                """;
            command.Parameters.AddWithValue("$record", record.Id.ToString());
            command.Parameters.AddWithValue("$item", item.ItemId);
            command.Parameters.AddWithValue("$name", item.ItemName);
            command.Parameters.AddWithValue("$quantity", item.Quantity);
            command.Parameters.AddWithValue("$price", item.UnitPrice);
            command.Parameters.AddWithValue("$unknown", item.IsUnknown ? 1 : 0);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM loot_records WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private static LootRecord? LoadRecord(SqliteConnection connection, Guid id)
    {
        LootRecord? record = null;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT character_id, free_company_id, date, created_at, note
                  FROM loot_records
                 WHERE id = $id;
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                record = new LootRecord
                {
                    Id = id,
                    CharacterId = Guid.Parse(reader.GetString(0)),
                    FreeCompanyId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
                    Date = DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                    Note = reader.GetString(4),
                };
            }
        }

        if (record is null)
            return null;

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT item_id, item_name, quantity, unit_price, is_unknown
                  FROM loot_items
                 WHERE record_id = $record
                 ORDER BY id;
                """;
            command.Parameters.AddWithValue("$record", id.ToString());
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                record.Items.Add(new LootItem
                {
                    ItemId = (uint)reader.GetInt64(0),
                    ItemName = reader.GetString(1),
                    Quantity = reader.GetInt32(2),
                    UnitPrice = reader.GetInt32(3),
                    IsUnknown = reader.GetInt64(4) != 0,
                });
            }
        }

        return record;
    }
}
