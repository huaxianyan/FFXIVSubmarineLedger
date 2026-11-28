using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

/// <summary>
/// 归属解析：日志行里的角色名/艇名怎么对回已登记的对象。
/// 多角色时每行的收益各归各的，对不上的必须空着等人选，不能猜、不能新建。
/// </summary>
public sealed class OwnershipResolverTests
{
    private static readonly CharacterRef Tiina =
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "Tiina Branford", "Titan");

    private static readonly CharacterRef Tilina =
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), "Tilina Branford", "Titan");

    private static readonly CharacterRef Alt =
        new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003"), "Tiina Alt", "Garuda");

    /// <summary>Tiina 与 Alt 名下是 Strawberry/Blueberry，Tilina 名下是 Cranberry。</summary>
    private static OwnershipResolver Resolver(params CharacterRef[] characters)
    {
        List<(Guid, string)> submarines = [];

        foreach (CharacterRef character in characters)
        {
            string[] names = character.Name == "Tilina Branford"
                ? ["Cranberry"]
                : ["Strawberry", "Blueberry"];

            submarines.AddRange(names.Select(name => (character.Id, name)));
        }

        return new OwnershipResolver(characters, submarines);
    }

    [Fact]
    public void EachLineGoesToItsOwnCharacter()
    {
        OwnershipResolver resolver = Resolver(Tiina, Tilina);

        Assert.Equal(Tiina.Id, resolver.Resolve("Tiina Branford", "Strawberry").Character?.Id);
        Assert.Equal(Tilina.Id, resolver.Resolve("Tilina Branford", "Cranberry").Character?.Id);
    }

    [Fact]
    public void SubmarinesAreResolvedWithinTheCharacterOnly()
    {
        OwnershipResolver resolver = Resolver(Tiina, Tilina);

        // Blueberry 在 Tiina 名下、不在 Tilina 名下：不能跨角色对过去，按新出现的艇留着。
        Assert.Equal("Blueberry", resolver.Resolve("Tilina Branford", "Blueberry").SubmarineName);
        Assert.DoesNotContain("Blueberry", resolver.SubmarinesOf(Tilina.Id));

        // 本角色名下读歪一个字母 → 对回去并给出提示。
        Ownership corrected = resolver.Resolve("Tiina Branford", "Strawberrv");
        Assert.Equal("Strawberry", corrected.SubmarineName);
        Assert.NotEqual(string.Empty, corrected.SubmarineNote);
    }

    [Fact]
    public void CharacterNameReadOneLetterOffIsCorrected()
    {
        OwnershipResolver resolver = Resolver(Tiina, Tilina);

        Ownership ownership = resolver.Resolve("Tiina Branfrod", "Strawberry");

        Assert.Equal(Tiina.Id, ownership.Character?.Id);
        Assert.NotEqual(string.Empty, ownership.CharacterNote);
    }

    [Fact]
    public void UnknownCharacterIsLeftEmptyAndNotCreated()
    {
        OwnershipResolver resolver = Resolver(Tiina);

        Ownership ownership = resolver.Resolve("Somebody Else", "Strawberry");

        Assert.Null(ownership.Character);
        Assert.NotEqual(string.Empty, ownership.CharacterNote);
        Assert.Single(resolver.Characters);
    }

    [Fact]
    public void SameNameOnTwoWorldsIsLeftForTheHuman()
    {
        CharacterRef twin = Tiina with { Id = Guid.NewGuid(), World = "Garuda" };
        OwnershipResolver resolver = Resolver(Tiina, twin);

        Ownership ownership = resolver.Resolve("Tiina Branford", "Strawberry");

        Assert.Null(ownership.Character);
        Assert.Contains("Garuda", ownership.CharacterNote, StringComparison.Ordinal);
    }

    [Fact]
    public void SubmarineIsNotGuessedBeforeTheCharacterIsKnown()
    {
        OwnershipResolver resolver = Resolver(Tiina);

        Ownership ownership = resolver.Resolve(null, "Strawberrv");

        Assert.Null(ownership.Character);
        Assert.Null(ownership.SubmarineName);
    }

    [Fact]
    public void MissingNamesGiveNoNote()
    {
        OwnershipResolver resolver = Resolver(Tiina);

        Ownership ownership = resolver.Resolve(null, null);

        Assert.Null(ownership.Character);
        Assert.Null(ownership.SubmarineName);
        Assert.Equal(string.Empty, ownership.CharacterNote);
        Assert.Equal(string.Empty, ownership.SubmarineNote);
    }

    [Fact]
    public void SubmarineNamesAreScopedToTheirCharacter()
    {
        OwnershipResolver resolver = Resolver(Tiina, Alt);

        Assert.Equal(["Blueberry", "Strawberry"], resolver.SubmarinesOf(Tiina.Id));
        Assert.Equal(["Blueberry", "Strawberry"], resolver.SubmarinesOf(Alt.Id));
        Assert.Empty(resolver.SubmarinesOf(Guid.NewGuid()));
    }
}
