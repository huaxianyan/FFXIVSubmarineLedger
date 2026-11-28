using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Names;

namespace FFXIVSubmarineLedger.Application.Records;

/// <summary>一个可以归属的角色。</summary>
/// <param name="Id">角色 ID。</param>
/// <param name="Name">角色名，日志行里出现的就是这一段。</param>
/// <param name="World">服务器名，可能为空。</param>
public sealed record CharacterRef(Guid Id, string Name, string World)
{
    /// <summary>「角色名 @ 服务器」，与角色管理页一致。</summary>
    public string Label => World.Length == 0 ? Name : $"{Name} @ {World}";
}

/// <summary>一行收益该记到谁名下、哪艘艇。空着的必须由人补上，不默认、不猜。</summary>
/// <param name="Character">对上的角色。认不出时为 <c>null</c>。</param>
/// <param name="CharacterNote">角色名需要人看一眼时的提示（读歪了、认不出、同名不同服）。</param>
/// <param name="SubmarineName">对上的艇名。没读到或没对上时为 <c>null</c>。</param>
/// <param name="SubmarineNote">艇名需要人看一眼时的提示。</param>
public sealed record Ownership(
    CharacterRef? Character,
    string CharacterNote,
    string? SubmarineName,
    string SubmarineNote);

/// <summary>
/// 把日志行里的名字对回已登记的角色与艇。
///
/// 两件事都不能默认：角色不对回去，整批收益会记到别人头上；艇不对回去会凭空造艇
/// （实测把 Strawberry 读成 Strawberrv 就多出一艘）。所以只在唯一一个名字足够近时
/// 才改写，分不清的一律置空并给出候选，由人在确认行里选。日志里出现没登记过的名字
/// 同样置空——读歪一个字就自动新建，等于把错的当真的。
///
/// 艇名只在角色范围内唯一，所以先认角色再对艇；角色没认出来时艇也不对。
/// </summary>
public sealed class OwnershipResolver
{
    private readonly IReadOnlyList<CharacterRef> _characters;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<string>> _submarines;

    /// <param name="characters">启用中的角色。</param>
    /// <param name="submarines">各角色名下的艇。</param>
    public OwnershipResolver(
        IReadOnlyList<CharacterRef> characters,
        IEnumerable<(Guid CharacterId, string Name)> submarines)
    {
        ArgumentNullException.ThrowIfNull(characters);
        ArgumentNullException.ThrowIfNull(submarines);

        _characters = characters;

        _submarines = submarines
            .Where(x => x.Name.Length > 0)
            .GroupBy(x => x.CharacterId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(x => x.Name).Order(StringComparer.Ordinal)]);
    }

    /// <summary>可归属的角色。</summary>
    public IReadOnlyList<CharacterRef> Characters => _characters;

    /// <summary>某个角色名下的艇名（按名字排序）。</summary>
    public IReadOnlyList<string> SubmarinesOf(Guid characterId) =>
        _submarines.GetValueOrDefault(characterId, []);

    public Ownership Resolve(string? characterName, string? submarineName)
    {
        (CharacterRef? character, string characterNote) = ResolveCharacter(characterName);
        (string? submarine, string submarineNote) = ResolveSubmarine(submarineName, character);
        return new Ownership(character, characterNote, submarine, submarineNote);
    }

    private (CharacterRef? Character, string Note) ResolveCharacter(string? read)
    {
        NameGuess guess = NameMatcher.Guess(read, [.. _characters.Select(x => x.Name)]);

        switch (guess.Kind)
        {
            case NameMatchKind.Missing:
                return (null, string.Empty);

            case NameMatchKind.Exact:
            case NameMatchKind.Corrected:
            {
                List<CharacterRef> hits = [.. _characters.Where(x => x.Name == guess.Name)];

                // 同名不同服：日志里只有名字，分不清是哪一位。
                if (hits.Count != 1)
                    return (null, Loc.Format("records.characterAmbiguous", read!, Labels(hits)));

                return guess.Kind == NameMatchKind.Corrected
                    ? (hits[0], Loc.Format("records.characterCorrected", read!, hits[0].Label))
                    : (hits[0], string.Empty);
            }

            case NameMatchKind.Ambiguous:
                return (null, Loc.Format(
                    "records.characterAmbiguous",
                    read!,
                    Labels([.. _characters.Where(x => guess.NearNames.Contains(x.Name))])));

            default:
                return (null, Loc.Format("records.characterUnknown", read!));
        }
    }

    private (string? Name, string Note) ResolveSubmarine(string? read, CharacterRef? character)
    {
        if (string.IsNullOrEmpty(read) || character is null)
            return (null, string.Empty);

        NameGuess guess = NameMatcher.Guess(read, SubmarinesOf(character.Id));

        return guess.Kind switch
        {
            NameMatchKind.Exact => (guess.Name, string.Empty),
            NameMatchKind.Corrected => (guess.Name, Loc.Format("records.subCorrected", read, guess.Name!)),
            NameMatchKind.Ambiguous => (null, Loc.Format("records.subAmbiguous", read, string.Join(" / ", guess.NearNames))),
            _ => (read, string.Empty),
        };
    }

    private static string Labels(IReadOnlyList<CharacterRef> characters) =>
        characters.Count == 0 ? string.Empty : string.Join(" / ", characters.Select(x => x.Label));
}
