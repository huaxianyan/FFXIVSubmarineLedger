using System.Globalization;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Backup;
using Microsoft.Data.Sqlite;

namespace FFXIVSubmarineLedger.Infrastructure.Persistence;

/// <summary>
/// 备份读写直接在表上做，不走仓储：导入要求「要么全换，要么一个字节都不动」，
/// 这必须是单个事务，而仓储按记录一条条写，凑不出这个保证。
/// </summary>
public sealed class SqliteBackupStore(SqliteDatabase database) : IBackupStore
{
    public Task<BackupDocument> ExportAsync(CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();

        List<BackupCharacter> characters = ReadCharacters(connection);
        List<BackupFreeCompany> freeCompanies = ReadFreeCompanies(connection);
        List<BackupSubmarine> submarines = ReadSubmarines(connection);
        List<BackupLootRecord> records = ReadRecords(connection);

        return Task.FromResult(new BackupDocument(
            BackupDocument.CurrentVersion,
            DateTimeOffset.Now,
            characters,
            freeCompanies,
            submarines,
            records));
    }

    public Task ImportAsync(BackupDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            // 子表先删，避免外键与残留行挡住。
            Execute(connection, transaction, "DELETE FROM loot_items;");
            Execute(connection, transaction, "DELETE FROM loot_records;");
            Execute(connection, transaction, "DELETE FROM submarines;");
            Execute(connection, transaction, "DELETE FROM free_companies;");
            Execute(connection, transaction, "DELETE FROM characters;");

            foreach (BackupCharacter character in document.Characters)
                InsertCharacter(connection, transaction, character);

            foreach (BackupFreeCompany company in document.FreeCompanies)
                InsertFreeCompany(connection, transaction, company);

            foreach (BackupSubmarine submarine in document.Submarines)
                InsertSubmarine(connection, transaction, submarine);

            foreach (BackupLootRecord record in document.Records)
                InsertRecord(connection, transaction, record);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return Task.CompletedTask;
    }

    private static List<BackupCharacter> ReadCharacters(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, world, is_active, is_default, created_at
              FROM characters
             ORDER BY created_at, name;
            """;

        var result = new List<BackupCharacter>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BackupCharacter(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3) != 0,
                reader.GetInt64(4) != 0,
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private static List<BackupFreeCompany> ReadFreeCompanies(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, name, character_id, created_at
              FROM free_companies
             ORDER BY created_at, name;
            """;

        var result = new List<BackupFreeCompany>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BackupFreeCompany(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                Guid.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private static List<BackupSubmarine> ReadSubmarines(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, character_id, name, created_at
              FROM submarines
             ORDER BY name;
            """;

        var result = new List<BackupSubmarine>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BackupSubmarine(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private static List<BackupLootRecord> ReadRecords(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, character_id, free_company_id, submarine_id, date, created_at, note
              FROM loot_records
             ORDER BY date, created_at;
            """;

        var result = new List<BackupLootRecord>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            Guid id = Guid.Parse(reader.GetString(0));

            result.Add(new BackupLootRecord(
                id,
                Guid.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
                DateOnly.ParseExact(reader.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
                reader.GetString(6),
                ReadItems(connection, id)));
        }

        return result;
    }

    private static List<BackupLootItem> ReadItems(SqliteConnection connection, Guid recordId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT item_id, item_name, quantity, is_high_quality, unit_price, is_unknown
              FROM loot_items
             WHERE record_id = $record
             ORDER BY id;
            """;
        command.Parameters.AddWithValue("$record", recordId.ToString());

        var result = new List<BackupLootItem>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BackupLootItem(
                (uint)reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt64(3) != 0,
                reader.GetInt32(4),
                reader.GetInt64(5) != 0));
        }

        return result;
    }

    private static void InsertCharacter(SqliteConnection connection, SqliteTransaction transaction, BackupCharacter character)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO characters (id, name, world, is_active, is_default, created_at)
            VALUES ($id, $name, $world, $active, $default, $created);
            """;
        command.Parameters.AddWithValue("$id", character.Id.ToString());
        command.Parameters.AddWithValue("$name", character.Name);
        command.Parameters.AddWithValue("$world", character.World);
        command.Parameters.AddWithValue("$active", character.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$default", character.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("$created", character.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private static void InsertFreeCompany(SqliteConnection connection, SqliteTransaction transaction, BackupFreeCompany company)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO free_companies (id, name, character_id, created_at)
            VALUES ($id, $name, $character, $created);
            """;
        command.Parameters.AddWithValue("$id", company.Id.ToString());
        command.Parameters.AddWithValue("$name", company.Name);
        command.Parameters.AddWithValue("$character", company.CharacterId.ToString());
        command.Parameters.AddWithValue("$created", company.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private static void InsertSubmarine(SqliteConnection connection, SqliteTransaction transaction, BackupSubmarine submarine)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO submarines (id, character_id, name, created_at)
            VALUES ($id, $character, $name, $created);
            """;
        command.Parameters.AddWithValue("$id", submarine.Id.ToString());
        command.Parameters.AddWithValue("$character", submarine.CharacterId.ToString());
        command.Parameters.AddWithValue("$name", submarine.Name);
        command.Parameters.AddWithValue("$created", submarine.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private static void InsertRecord(SqliteConnection connection, SqliteTransaction transaction, BackupLootRecord record)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO loot_records (id, character_id, free_company_id, submarine_id, date, created_at, note)
                VALUES ($id, $character, $company, $submarine, $date, $created, $note);
                """;
            command.Parameters.AddWithValue("$id", record.Id.ToString());
            command.Parameters.AddWithValue("$character", record.CharacterId.ToString());
            command.Parameters.AddWithValue("$company", record.FreeCompanyId is { } company ? company.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$submarine", record.SubmarineId is { } submarine ? submarine.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$date", record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$created", record.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$note", record.Note);
            command.ExecuteNonQuery();
        }

        foreach (BackupLootItem item in record.Items)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO loot_items (record_id, item_id, item_name, quantity, is_high_quality, unit_price, is_unknown)
                VALUES ($record, $item, $name, $quantity, $hq, $price, $unknown);
                """;
            command.Parameters.AddWithValue("$record", record.Id.ToString());
            command.Parameters.AddWithValue("$item", item.ItemId);
            command.Parameters.AddWithValue("$name", item.ItemName);
            command.Parameters.AddWithValue("$quantity", item.Quantity);
            command.Parameters.AddWithValue("$hq", item.IsHighQuality ? 1 : 0);
            command.Parameters.AddWithValue("$price", item.UnitPrice);
            command.Parameters.AddWithValue("$unknown", item.IsUnknown ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
