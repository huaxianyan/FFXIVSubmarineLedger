using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Domain.Tests.Characters;

/// <summary>
/// 角色卡整张保存：角色名、服务器、名下每艘艇的名字一起校验一起写。
/// 分两次存最容易存半张——所以要先证「有一处不合法时一个字节都没写」。
/// </summary>
public sealed class RosterEditorTests
{
    [Fact]
    public async Task SavesTheCharacterAndEverySubmarineTogether()
    {
        FakeCharacters characters = new();
        FakeSubmarines submarines = new();
        Character character = characters.Add("Tiina Branford", "Titan");
        Submarine first = submarines.Add(character.Id, "Blackberry");
        Submarine second = submarines.Add(character.Id, "Blueberry");
        RosterEditor editor = new(characters, submarines);

        await editor.SaveAsync(
            character.Id,
            "Tilina Branford",
            "Garuda",
            [new RosterSubmarine(first.Id, "Strawberry"), new RosterSubmarine(second.Id, "Cranberry")]);

        Assert.Equal("Tilina Branford", characters.Items.Single().Name);
        Assert.Equal("Garuda", characters.Items.Single().World);
        Assert.Equal(["Cranberry", "Strawberry"], submarines.Items.Select(x => x.Name).Order());
        Assert.Equal(1, characters.Updates);
        Assert.Equal(2, submarines.Updates);
    }

    [Fact]
    public async Task WritesNothingWhenOneSubmarineNameIsRejected()
    {
        FakeCharacters characters = new();
        FakeSubmarines submarines = new();
        Character character = characters.Add("Tiina Branford", "Titan");
        Submarine first = submarines.Add(character.Id, "Blackberry");
        Submarine second = submarines.Add(character.Id, "Blueberry");
        RosterEditor editor = new(characters, submarines);

        // 两艘写成一个名字：整张卡退回去，角色名和第一艘艇名都不能已经写进去。
        await Assert.ThrowsAsync<ValidationException>(() => editor.SaveAsync(
            character.Id,
            "Tilina Branford",
            "Titan",
            [new RosterSubmarine(first.Id, "Same"), new RosterSubmarine(second.Id, "Same")]));

        Assert.Equal("Tiina Branford", characters.Items.Single().Name);
        Assert.Equal(0, characters.Updates);
        Assert.Equal(0, submarines.Updates);
        Assert.Equal("Blackberry", submarines.Items.Single(x => x.Id == first.Id).Name);
    }

    [Fact]
    public async Task WritesNothingWhenTheCharacterNameIsTaken()
    {
        FakeCharacters characters = new();
        FakeSubmarines submarines = new();
        Character mine = characters.Add("Tiina Branford", "Titan");
        characters.Add("Tiina Alt", "Titan");
        Submarine submarine = submarines.Add(mine.Id, "Blackberry");
        RosterEditor editor = new(characters, submarines);

        await Assert.ThrowsAsync<ValidationException>(() => editor.SaveAsync(
            mine.Id,
            "Tiina Alt",
            "Titan",
            [new RosterSubmarine(submarine.Id, "Strawberry")]));

        Assert.Equal(0, characters.Updates);
        Assert.Equal(0, submarines.Updates);
    }

    [Fact]
    public async Task RefusesAnEmptySubmarineName()
    {
        FakeCharacters characters = new();
        FakeSubmarines submarines = new();
        Character character = characters.Add("Tiina Branford", "Titan");
        Submarine submarine = submarines.Add(character.Id, "Blackberry");
        RosterEditor editor = new(characters, submarines);

        await Assert.ThrowsAsync<ValidationException>(() => editor.SaveAsync(
            character.Id,
            "Tiina Branford",
            "Titan",
            [new RosterSubmarine(submarine.Id, "  ")]));
    }

    [Fact]
    public async Task KeepsTheSubmarineIdSoHistoryFollowsTheRename()
    {
        FakeCharacters characters = new();
        FakeSubmarines submarines = new();
        Character character = characters.Add("Tiina Branford", "Titan");
        Submarine submarine = submarines.Add(character.Id, "Blackberry");
        RosterEditor editor = new(characters, submarines);

        await editor.SaveAsync(
            character.Id,
            "Tiina Branford",
            "Titan",
            [new RosterSubmarine(submarine.Id, "Strawberry")]);

        // 名字换了，ID 没换：历史记录还挂在同一艘艇上。
        Assert.Contains(submarines.Items, x => x.Id == submarine.Id && x.Name == "Strawberry");
    }

    private sealed class FakeCharacters : ICharacterRepository
    {
        public List<Character> Items { get; } = [];

        public int Updates { get; private set; }

        public Character Add(string name, string world)
        {
            Character character = new() { Name = name, World = world };
            Items.Add(character);
            return character;
        }

        public Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Character>>([.. Items]);

        public Task<Character?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(Character character, CancellationToken ct = default)
        {
            Items.Add(character);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Character character, CancellationToken ct = default)
        {
            Updates++;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSubmarines : ISubmarineRepository
    {
        public List<Submarine> Items { get; } = [];

        public int Updates { get; private set; }

        public Submarine Add(Guid characterId, string name)
        {
            Submarine submarine = new() { CharacterId = characterId, Name = name };
            Items.Add(submarine);
            return submarine;
        }

        public Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Submarine>>(
                [.. Items.Where(x => characterId is null || x.CharacterId == characterId)]);

        public Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.CharacterId == characterId && x.Name == name));

        public Task AddAsync(Submarine submarine, CancellationToken ct = default)
        {
            Items.Add(submarine);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Submarine submarine, CancellationToken ct = default)
        {
            Updates++;
            return Task.CompletedTask;
        }
    }
}
