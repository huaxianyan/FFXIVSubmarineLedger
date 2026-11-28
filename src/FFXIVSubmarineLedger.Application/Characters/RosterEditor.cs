using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Application.Characters;

/// <summary>一次保存里的一艘艇：认 ID，不认名字（名字正在改）。</summary>
public sealed record RosterSubmarine(Guid Id, string Name);

/// <summary>
/// 角色卡整张保存：角色名、服务器、每艘艇的名字**一起校验、一起写入**。
///
/// 分两次存（先存角色再逐艘存艇）很容易存半张：改到第三艘时撞名报错，前两艘已经写进去了，
/// 剩下那艘没人管。所以先把所有名字检查完，再动第一个字节。
/// </summary>
public sealed class RosterEditor(ICharacterRepository characters, ISubmarineRepository submarines)
{
    /// <param name="characterId">正在编辑的角色。</param>
    /// <param name="name">角色名。</param>
    /// <param name="world">服务器，可空。</param>
    /// <param name="roster">这个角色名下的艇（ID + 新名字）。</param>
    public async Task SaveAsync(
        Guid characterId,
        string name,
        string world,
        IReadOnlyList<RosterSubmarine> roster,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roster);

        name = name?.Trim() ?? string.Empty;
        world = world?.Trim() ?? string.Empty;

        if (name.Length == 0)
            throw new ValidationException(Loc.Get("validation.nameRequired"));

        IReadOnlyList<Character> all = await characters.ListAsync(ct).ConfigureAwait(false);
        Character target = all.FirstOrDefault(x => x.Id == characterId)
            ?? throw new ValidationException(Loc.Get("validation.characterMissing"));

        // 同名同服的两个角色会让归属解析变成「分不清」，必须在写库前挡住。
        if (all.Any(x => x.Id != characterId
                         && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                         && string.Equals(x.World, world, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException(Loc.Format("validation.duplicate", name));

        IReadOnlyList<Submarine> existing = await submarines.ListAsync(characterId, ct).ConfigureAwait(false);

        Dictionary<Guid, string> renames = [];
        HashSet<string> taken = new(StringComparer.Ordinal);

        foreach (RosterSubmarine item in roster)
        {
            string submarineName = item.Name?.Trim() ?? string.Empty;

            if (submarineName.Length == 0)
                throw new ValidationException(Loc.Get("validation.submarineNameRequired"));

            if (existing.All(x => x.Id != item.Id))
                throw new ValidationException(Loc.Get("validation.submarineMissing"));

            // 卡上两艘艇写成同一个名字也算撞名。
            if (!taken.Add(submarineName))
                throw new ValidationException(Loc.Format("validation.submarineDuplicate", submarineName));

            renames[item.Id] = submarineName;
        }

        // 校验全过了才开始写。
        if (!string.Equals(target.Name, name, StringComparison.Ordinal)
            || !string.Equals(target.World, world, StringComparison.Ordinal))
        {
            target.Name = name;
            target.World = world;
            await characters.UpdateAsync(target, ct).ConfigureAwait(false);
        }

        foreach (Submarine submarine in existing)
        {
            if (!renames.TryGetValue(submarine.Id, out string? next)
                || string.Equals(next, submarine.Name, StringComparison.Ordinal))
                continue;

            submarine.Name = next;
            await submarines.UpdateAsync(submarine, ct).ConfigureAwait(false);
        }
    }
}
