using System.Globalization;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Domain.Characters;
using Microsoft.Data.Sqlite;

namespace FFXIVSubLedger.Infrastructure.Persistence;

public sealed class SqliteSubmarineRepository(SqliteDatabase database) : ISubmarineRepository
{
    public Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT id, character_id, name, created_at
              FROM submarines
             WHERE $character IS NULL OR character_id = $character
             ORDER BY name;
            """;
        command.Parameters.AddWithValue("$character", characterId is { } id ? id.ToString() : DBNull.Value);

        var result = new List<Submarine>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Submarine
            {
                Id = Guid.Parse(reader.GetString(0)),
                CharacterId = Guid.Parse(reader.GetString(1)),
                Name = reader.GetString(2),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            });
        }

        return Task.FromResult<IReadOnlyList<Submarine>>(result);
    }

    public Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default)
    {
        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT id, character_id, name, created_at
              FROM submarines
             WHERE character_id = $character AND name = $name;
            """;
        command.Parameters.AddWithValue("$character", characterId.ToString());
        command.Parameters.AddWithValue("$name", name);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            return Task.FromResult<Submarine?>(null);

        return Task.FromResult<Submarine?>(new Submarine
        {
            Id = Guid.Parse(reader.GetString(0)),
            CharacterId = Guid.Parse(reader.GetString(1)),
            Name = reader.GetString(2),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
        });
    }

    public Task AddAsync(Submarine submarine, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submarine);

        using SqliteConnection connection = database.Open();
        using SqliteCommand command = connection.CreateCommand();

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

        return Task.CompletedTask;
    }
}
