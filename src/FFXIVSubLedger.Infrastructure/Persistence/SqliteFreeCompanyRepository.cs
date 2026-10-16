using System.Globalization;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Domain.Characters;
using Microsoft.Data.Sqlite;

namespace FFXIVSubLedger.Infrastructure.Persistence;

public sealed class SqliteFreeCompanyRepository(SqliteDatabase database) : IFreeCompanyRepository
{
    private const string SelectColumns = "id, name, character_id, created_at";

    public Task<IReadOnlyList<FreeCompany>> ListAsync(CancellationToken ct = default)
        => QueryAsync("SELECT " + SelectColumns + " FROM free_companies ORDER BY created_at;", null);

    public Task<IReadOnlyList<FreeCompany>> ListByCharacterAsync(Guid characterId, CancellationToken ct = default)
        => QueryAsync(
            "SELECT " + SelectColumns + " FROM free_companies WHERE character_id = $character ORDER BY created_at;",
            characterId.ToString());

    public Task AddAsync(FreeCompany freeCompany, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO free_companies (id, name, character_id, created_at)
            VALUES ($id, $name, $character, $created);
            """;
        Bind(command, freeCompany);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FreeCompany freeCompany, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE free_companies
               SET name = $name, character_id = $character
             WHERE id = $id;
            """;
        Bind(command, freeCompany);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM free_companies WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private Task<IReadOnlyList<FreeCompany>> QueryAsync(string sql, string? characterId)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (characterId is not null)
            command.Parameters.AddWithValue("$character", characterId);
        using SqliteDataReader reader = command.ExecuteReader();

        List<FreeCompany> result = [];
        while (reader.Read())
            result.Add(Map(reader));
        return Task.FromResult<IReadOnlyList<FreeCompany>>(result);
    }

    private static void Bind(SqliteCommand command, FreeCompany freeCompany)
    {
        command.Parameters.AddWithValue("$id", freeCompany.Id.ToString());
        command.Parameters.AddWithValue("$name", freeCompany.Name);
        command.Parameters.AddWithValue("$character", freeCompany.CharacterId.ToString());
        command.Parameters.AddWithValue("$created", freeCompany.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
    }

    private static FreeCompany Map(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Name = reader.GetString(1),
        CharacterId = Guid.Parse(reader.GetString(2)),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
    };
}
