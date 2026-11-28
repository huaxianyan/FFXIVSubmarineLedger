using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Application.Characters;

/// <summary>角色管理的业务规则。UI 不直接操作仓储。</summary>
public sealed class CharacterService(ICharacterRepository characters)
{
    public Task<IReadOnlyList<Character>> ListAsync(bool includeInactive, CancellationToken ct = default)
        => ListCoreAsync(includeInactive, ct);

    private async Task<IReadOnlyList<Character>> ListCoreAsync(bool includeInactive, CancellationToken ct)
    {
        IReadOnlyList<Character> all = await characters.ListAsync(ct).ConfigureAwait(false);
        return includeInactive
            ? all.OrderBy(x => x.CreatedAt).ToList()
            : all.Where(x => x.IsActive).OrderBy(x => x.CreatedAt).ToList();
    }

    public async Task<Character> AddAsync(string name, string world, CancellationToken ct = default)
    {
        name = name?.Trim() ?? string.Empty;
        world = world?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new ValidationException(Loc.Get("validation.nameRequired"));

        IReadOnlyList<Character> existing = await characters.ListAsync(ct).ConfigureAwait(false);
        if (existing.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                              && string.Equals(x.World, world, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException(Loc.Format("validation.duplicate", name));

        Character character = new()
        {
            Name = name,
            World = world,
        };
        await characters.AddAsync(character, ct).ConfigureAwait(false);
        return character;
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        Character character = await RequireAsync(id, ct).ConfigureAwait(false);
        character.IsActive = isActive;
        await characters.UpdateAsync(character, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await RequireAsync(id, ct).ConfigureAwait(false);
        await characters.DeleteAsync(id, ct).ConfigureAwait(false);
    }

    private async Task<Character> RequireAsync(Guid id, CancellationToken ct)
        => await characters.FindAsync(id, ct).ConfigureAwait(false)
           ?? throw new ValidationException(Loc.Get("validation.characterMissing"));
}
