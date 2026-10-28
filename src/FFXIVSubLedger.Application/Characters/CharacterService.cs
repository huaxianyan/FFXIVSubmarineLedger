using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Errors;
using FFXIVSubLedger.Domain.Characters;

namespace FFXIVSubLedger.Application.Characters;

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
            // 第一个角色自动成为默认，省掉一次设置。
            IsDefault = existing.Count == 0,
        };
        await characters.AddAsync(character, ct).ConfigureAwait(false);
        return character;
    }

    public async Task RenameAsync(Guid id, string name, string world, CancellationToken ct = default)
    {
        Character character = await RequireAsync(id, ct).ConfigureAwait(false);
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new ValidationException(Loc.Get("validation.nameRequired"));

        character.Name = name;
        character.World = world?.Trim() ?? string.Empty;
        await characters.UpdateAsync(character, ct).ConfigureAwait(false);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        IReadOnlyList<Character> all = await characters.ListAsync(ct).ConfigureAwait(false);
        if (all.All(x => x.Id != id))
            throw new ValidationException(Loc.Get("validation.characterMissing"));

        // 默认角色全局唯一。
        foreach (Character item in all)
        {
            bool shouldBeDefault = item.Id == id;
            if (item.IsDefault == shouldBeDefault)
                continue;
            item.IsDefault = shouldBeDefault;
            await characters.UpdateAsync(item, ct).ConfigureAwait(false);
        }
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        Character character = await RequireAsync(id, ct).ConfigureAwait(false);
        character.IsActive = isActive;

        // 停用的角色不能继续当默认，转给第一个仍然启用的角色。
        if (!isActive && character.IsDefault)
        {
            character.IsDefault = false;
            IReadOnlyList<Character> all = await characters.ListAsync(ct).ConfigureAwait(false);
            Character? next = all.FirstOrDefault(x => x.IsActive && x.Id != id);
            if (next is not null)
                next.IsDefault = true;
            if (next is not null)
                await characters.UpdateAsync(next, ct).ConfigureAwait(false);
        }

        await characters.UpdateAsync(character, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Character character = await RequireAsync(id, ct).ConfigureAwait(false);
        await characters.DeleteAsync(id, ct).ConfigureAwait(false);

        if (!character.IsDefault)
            return;

        IReadOnlyList<Character> remaining = await characters.ListAsync(ct).ConfigureAwait(false);
        Character? next = remaining.FirstOrDefault(x => x.IsActive) ?? remaining.FirstOrDefault();
        if (next is not null)
        {
            next.IsDefault = true;
            await characters.UpdateAsync(next, ct).ConfigureAwait(false);
        }
    }

    private async Task<Character> RequireAsync(Guid id, CancellationToken ct)
        => await characters.FindAsync(id, ct).ConfigureAwait(false)
           ?? throw new ValidationException(Loc.Get("validation.characterMissing"));
}
