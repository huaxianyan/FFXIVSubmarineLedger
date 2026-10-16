using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Errors;
using FFXIVSubLedger.Domain.Characters;

namespace FFXIVSubLedger.Domain.Tests;

public sealed class CharacterServiceTests
{
    [Fact]
    public async Task FirstCharacterBecomesDefault()
    {
        CharacterService service = new(new FakeCharacterRepository());

        Character created = await service.AddAsync("Alice", "红玉海");

        Assert.True(created.IsDefault);
    }

    [Fact]
    public async Task SecondCharacterIsNotDefault()
    {
        CharacterService service = new(new FakeCharacterRepository());
        await service.AddAsync("Alice", "红玉海");

        Character second = await service.AddAsync("Bob", "红玉海");

        Assert.False(second.IsDefault);
    }

    [Fact]
    public async Task EmptyNameIsRejected()
    {
        CharacterService service = new(new FakeCharacterRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.AddAsync("   ", "红玉海"));
    }

    [Fact]
    public async Task DuplicateNameOnSameWorldIsRejected()
    {
        CharacterService service = new(new FakeCharacterRepository());
        await service.AddAsync("Alice", "红玉海");

        await Assert.ThrowsAsync<ValidationException>(() => service.AddAsync("alice", "红玉海"));
    }

    [Fact]
    public async Task SameNameOnDifferentWorldIsAllowed()
    {
        CharacterService service = new(new FakeCharacterRepository());
        await service.AddAsync("Alice", "红玉海");

        Character other = await service.AddAsync("Alice", "神意之地");

        Assert.Equal("神意之地", other.World);
    }

    [Fact]
    public async Task SetDefaultClearsOtherDefaults()
    {
        FakeCharacterRepository repository = new();
        CharacterService service = new(repository);
        Character first = await service.AddAsync("Alice", "红玉海");
        Character second = await service.AddAsync("Bob", "红玉海");

        await service.SetDefaultAsync(second.Id);

        Assert.False((await repository.FindAsync(first.Id))!.IsDefault);
        Assert.True((await repository.FindAsync(second.Id))!.IsDefault);
    }

    [Fact]
    public async Task DeactivatingDefaultMovesDefaultToAnotherActiveCharacter()
    {
        FakeCharacterRepository repository = new();
        CharacterService service = new(repository);
        Character first = await service.AddAsync("Alice", "红玉海");
        Character second = await service.AddAsync("Bob", "红玉海");

        await service.SetActiveAsync(first.Id, false);

        Assert.False((await repository.FindAsync(first.Id))!.IsDefault);
        Assert.True((await repository.FindAsync(second.Id))!.IsDefault);
    }

    [Fact]
    public async Task DeletingDefaultMovesDefaultToRemainingCharacter()
    {
        FakeCharacterRepository repository = new();
        CharacterService service = new(repository);
        Character first = await service.AddAsync("Alice", "红玉海");
        Character second = await service.AddAsync("Bob", "红玉海");

        await service.DeleteAsync(first.Id);

        Assert.True((await repository.FindAsync(second.Id))!.IsDefault);
    }

    [Fact]
    public async Task ListCanExcludeInactiveCharacters()
    {
        CharacterService service = new(new FakeCharacterRepository());
        Character first = await service.AddAsync("Alice", "红玉海");
        await service.AddAsync("Bob", "红玉海");

        await service.SetActiveAsync(first.Id, false);

        Assert.Single(await service.ListAsync(includeInactive: false));
        Assert.Equal(2, (await service.ListAsync(includeInactive: true)).Count);
    }
}

internal sealed class FakeCharacterRepository : ICharacterRepository
{
    private readonly List<Character> _items = [];

    public Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Character>>(_items.ToList());

    public Task<Character?> FindAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task AddAsync(Character character, CancellationToken ct = default)
    {
        _items.Add(character);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Character character, CancellationToken ct = default)
    {
        int index = _items.FindIndex(x => x.Id == character.Id);
        if (index >= 0)
            _items[index] = character;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _items.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }
}
