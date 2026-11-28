using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Application.Characters;

/// <summary>一次保存里的一艘艇。<paramref name="Id"/> 为空表示这艘是新填的，还没有落库。</summary>
public sealed record RosterSubmarine(Guid? Id, string Name);

/// <summary>
/// 角色卡整张保存：角色名、服务器、每艘艇的名字**一起校验、一起写入**。
///
/// 分两次存（先存角色再逐艘存艇）很容易存半张：改到第三艘时撞名报错，前两艘已经写进去了，
/// 剩下那艘没人管。所以先把所有名字检查完，再动第一个字节。
///
/// 艇名允许手工填：玩家在游戏里给艇起了名，先登记过名字，之后日志读到就能对上，
/// 不用等第一次收艇才凭空冒出一艘。没名字的空位直接跳过，不当成错误。
/// </summary>
public sealed class RosterEditor(ICharacterRepository characters, ISubmarineRepository submarines)
{
    /// <summary>新建一个角色，顺带把他名下的艇名一起登记。空着的艇名跳过。</summary>
    public async Task<Character> AddAsync(
        string name,
        string world,
        IReadOnlyList<string> submarineNames,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(submarineNames);

        name = name?.Trim() ?? string.Empty;
        world = world?.Trim() ?? string.Empty;

        if (name.Length == 0)
            throw new ValidationException(Loc.Get("validation.nameRequired"));

        IReadOnlyList<Character> all = await characters.ListAsync(ct).ConfigureAwait(false);
        if (all.Any(x => Equals(x.Name, name) && Equals(x.World, world)))
            throw new ValidationException(Loc.Format("validation.duplicate", name));

        string[] taken = CheckSubmarineNames(submarineNames);

        Character character = new() { Name = name, World = world };
        await characters.AddAsync(character, ct).ConfigureAwait(false);

        foreach (string submarineName in taken)
            await submarines.AddAsync(new Submarine { CharacterId = character.Id, Name = submarineName }, ct)
                .ConfigureAwait(false);

        return character;
    }

    /// <param name="characterId">正在编辑的角色。</param>
    /// <param name="name">角色名。</param>
    /// <param name="world">服务器，可空。</param>
    /// <param name="roster">这个角色名下的艇：已有的带 ID，新填的 ID 为空。</param>
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

        foreach (RosterSubmarine item in roster)
        {
            if (item.Id is not { } id)
                continue;

            // 新填的艇身份是靠名字，名字必须真的属于这个角色。
            if (existing.All(x => x.Id != id))
                throw new ValidationException(Loc.Get("validation.submarineMissing"));

            // 已有的艇被清空名字是误操作，不当成「不改」悄悄放过。
            if ((item.Name?.Trim() ?? string.Empty).Length == 0)
                throw new ValidationException(Loc.Get("validation.submarineNameRequired"));
        }

        CheckSubmarineNames([.. roster.Select(x => x.Name)]);

        Dictionary<Guid, string> renames = [];
        foreach (RosterSubmarine item in roster)
        {
            if (item.Id is { } id)
                renames[id] = item.Name.Trim();
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

        // 只建卡上新填的那些；已有艇可能在同一次保存里改了名，不能按名字去重判断。
        foreach (RosterSubmarine item in roster)
        {
            if (item.Id is not null)
                continue;

            string submarineName = item.Name.Trim();
            if (submarineName.Length == 0)
                continue;

            await submarines.AddAsync(new Submarine { CharacterId = characterId, Name = submarineName }, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 检查一批艇名：空着的跳过（还没起名），有名字的必须互不重复。
    /// 返回去空白后的名字，保持原来的顺序。
    /// </summary>
    private static string[] CheckSubmarineNames(IReadOnlyList<string> names)
    {
        List<string> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string raw in names)
        {
            string submarineName = raw?.Trim() ?? string.Empty;
            if (submarineName.Length == 0)
                continue;

            if (!seen.Add(submarineName))
                throw new ValidationException(Loc.Format("validation.submarineDuplicate", submarineName));

            result.Add(submarineName);
        }

        return [.. result];
    }
}
