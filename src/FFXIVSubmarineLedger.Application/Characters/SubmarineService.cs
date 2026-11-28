using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Application.Characters;

/// <summary>
/// 潜艇的业务规则。艇名取自日志，玩家在游戏里改了名之后要能在角色页同步过来——
/// 名字对上了，之后识别到新名字还是同一艘，历史记录不会分裂。
/// </summary>
public sealed class SubmarineService(ISubmarineRepository submarines)
{
    public Task<IReadOnlyList<Submarine>> ListAsync(Guid characterId, CancellationToken ct = default)
        => submarines.ListAsync(characterId, ct);

    /// <summary>给一艘艇改名。艇名只在角色范围内唯一，撞名就拒绝，不悄悄并成一艘。</summary>
    public async Task RenameAsync(Guid id, Guid characterId, string name, CancellationToken ct = default)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new ValidationException(Loc.Get("validation.submarineNameRequired"));

        IReadOnlyList<Submarine> all = await submarines.ListAsync(characterId, ct).ConfigureAwait(false);

        Submarine target = all.FirstOrDefault(x => x.Id == id)
            ?? throw new ValidationException(Loc.Get("validation.submarineMissing"));

        if (all.Any(x => x.Id != id && string.Equals(x.Name, name, StringComparison.Ordinal)))
            throw new ValidationException(Loc.Format("validation.submarineDuplicate", name));

        if (string.Equals(target.Name, name, StringComparison.Ordinal))
            return;

        target.Name = name;
        await submarines.UpdateAsync(target, ct).ConfigureAwait(false);
    }
}
