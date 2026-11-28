using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Domain.Tests.Characters;

/// <summary>
/// 给艇改名。游戏里改过名之后要在角色页同步过来，名字对上才能继续归到同一艘艇上；
/// 撞名必须挡住——两艘同名艇会让之后的归属再也分不清。
/// </summary>
public sealed class SubmarineServiceTests
{
    private static readonly Guid OwnerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public async Task RenamesTheSubmarineInPlace()
    {
        FakeSubmarines submarines = new();
        Submarine target = submarines.Add(OwnerId, "Blackberry");
        SubmarineService service = new(submarines);

        await service.RenameAsync(target.Id, OwnerId, "  Blueberry  ");

        Assert.Equal("Blueberry", (await submarines.ListAsync(OwnerId)).Single(x => x.Id == target.Id).Name);
    }

    [Fact]
    public async Task RefusesANameAlreadyUsedInTheSameCharacter()
    {
        FakeSubmarines submarines = new();
        Submarine first = submarines.Add(OwnerId, "Blackberry");
        submarines.Add(OwnerId, "Blueberry");
        SubmarineService service = new(submarines);

        await Assert.ThrowsAsync<ValidationException>(() => service.RenameAsync(first.Id, OwnerId, "Blueberry"));
    }

    [Fact]
    public async Task RefusesAnEmptyName()
    {
        FakeSubmarines submarines = new();
        Submarine target = submarines.Add(OwnerId, "Blackberry");
        SubmarineService service = new(submarines);

        await Assert.ThrowsAsync<ValidationException>(() => service.RenameAsync(target.Id, OwnerId, "   "));
    }

    [Fact]
    public async Task KeepsTheSameIdSoHistoryStaysAttached()
    {
        FakeSubmarines submarines = new();
        Submarine target = submarines.Add(OwnerId, "Blackberry");
        SubmarineService service = new(submarines);

        await service.RenameAsync(target.Id, OwnerId, "Strawberry");

        // ID 不变：改名之后历史记录还挂在同一艘艇上。
        Assert.Contains(await submarines.ListAsync(OwnerId), x => x.Id == target.Id);
    }

    private sealed class FakeSubmarines : ISubmarineRepository
    {
        private readonly List<Submarine> _items = [];

        public Submarine Add(Guid characterId, string name)
        {
            Submarine submarine = new() { CharacterId = characterId, Name = name };
            _items.Add(submarine);
            return submarine;
        }

        public Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Submarine>>(
                [.. _items.Where(x => characterId is null || x.CharacterId == characterId)]);

        public Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.CharacterId == characterId && x.Name == name));

        public Task AddAsync(Submarine submarine, CancellationToken ct = default)
        {
            _items.Add(submarine);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Submarine submarine, CancellationToken ct = default) => Task.CompletedTask;
    }
}
