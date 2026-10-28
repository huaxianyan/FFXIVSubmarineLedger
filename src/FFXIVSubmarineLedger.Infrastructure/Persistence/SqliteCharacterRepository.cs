using System.Globalization;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Domain.Characters;
using Microsoft.Data.Sqlite;

namespace FFXIVSubmarineLedger.Infrastructure.Persistence;

public sealed class SqliteCharacterRepository(SqliteDatabase database) : ICharacterRepository
{
    private const string SelectColumns = "id, name, world, is_active, is_default, created_at";

    public Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM characters ORDER BY created_at;";
        using SqliteDataReader reader = command.ExecuteReader();

        List<Character> result = [];
        while (reader.Read())
            result.Add(Map(reader));
        return Task.FromResult<IReadOnlyList<Character>>(result);
    }

    public Task<Character?> FindAsync(Guid id, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM characters WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        using SqliteDataReader reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? Map(reader) : null);
    }

    public Task AddAsync(Character character, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO characters (id, name, world, is_active, is_default, created_at)
            VALUES ($id, $name, $world, $active, $default, $created);
            """;
        Bind(command, character);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Character character, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE characters
               SET name = $name, world = $world, is_active = $active, is_default = $default
             WHERE id = $id;
            """;
        Bind(command, character);
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM characters WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private static void Bind(SqliteCommand command, Character character)
    {
        command.Parameters.AddWithValue("$id", character.Id.ToString());
        command.Parameters.AddWithValue("$name", character.Name);
        command.Parameters.AddWithValue("$world", character.World);
        command.Parameters.AddWithValue("$active", character.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$default", character.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("$created", character.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
    }

    private static Character Map(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        Name = reader.GetString(1),
        World = reader.GetString(2),
        IsActive = reader.GetInt64(3) != 0,
        IsDefault = reader.GetInt64(4) != 0,
        CreatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
    };
}
