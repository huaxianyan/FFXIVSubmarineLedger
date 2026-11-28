using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

public sealed partial class CharacterManagerViewModel(
    CharacterService characters,
    RosterEditor roster,
    ISubmarineRepository submarines,
    Func<TopLevel?> topLevelProvider) : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<CharacterRowViewModel> _rows = [];

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _newWorld = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasCharacters;

    [ObservableProperty]
    private string _countLabel = string.Empty;

    /// <summary>有操作反馈时才显示提示条，避免空出一块留白。</summary>
    public bool HasStatus => StatusMessage.Length > 0;

    public async Task LoadAsync()
    {
        IReadOnlyList<Character> all = await characters.ListAsync(includeInactive: true).ConfigureAwait(true);

        // 一次把全部艇取回来再按角色分组：角色不多，别为每张卡各跑一次查询。
        IReadOnlyList<Submarine> everySubmarine = await submarines.ListAsync().ConfigureAwait(true);
        var byCharacter = everySubmarine.ToLookup(x => x.CharacterId);

        Rows = new ObservableCollection<CharacterRowViewModel>(
            all.Select(x => new CharacterRowViewModel(x, this)));

        foreach (CharacterRowViewModel row in Rows)
            row.SetSubmarines([.. byCharacter[row.Id]]);

        HasCharacters = Rows.Count > 0;
        CountLabel = Rows.Count == 0 ? string.Empty : Loc.Format("characters.countLabel", Rows.Count);
    }

    /// <summary>换语言后刷新页内由代码拼出来的文字。</summary>
    public void Relocalize()
    {
        CountLabel = Rows.Count == 0 ? string.Empty : Loc.Format("characters.countLabel", Rows.Count);

        foreach (CharacterRowViewModel row in Rows)
            row.Relocalize();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        try
        {
            await characters.AddAsync(NewName, NewWorld).ConfigureAwait(true);
            NewName = string.Empty;
            NewWorld = string.Empty;
            StatusMessage = Loc.Get("characters.added");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (ValidationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    /// <summary>
    /// 从剪贴板填名字与服务器。游戏里复制出来的常见是「角色名@服务器」，
    /// 手打两个字段太麻烦，所以粘贴时把两段拆开分别填进去。
    /// </summary>
    [RelayCommand]
    private async Task PasteAsync()
    {
        if (topLevelProvider()?.Clipboard is not { } clipboard)
            return;

        string? text = await clipboard.TryGetTextAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusMessage = Loc.Get("characters.pasteEmpty");
            return;
        }

        (string name, string world) = ParseCharacterText(text);

        if (name.Length == 0)
        {
            StatusMessage = Loc.Get("characters.pasteEmpty");
            return;
        }

        NewName = name;
        NewWorld = world;
        StatusMessage = Loc.Format("characters.pasted", world.Length == 0 ? name : $"{name} @ {world}");
    }

    /// <summary>
    /// 拆「角色名@服务器」。分隔符认 <c>@</c> 与全角 <c>＠</c>，两边空白削掉；
    /// 只贴了一个名字也能用。
    /// </summary>
    public static (string Name, string World) ParseCharacterText(string text)
    {
        string line = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        line = line.Trim();

        int at = line.IndexOf('@', StringComparison.Ordinal);
        if (at < 0)
            at = line.IndexOf('＠', StringComparison.Ordinal);

        return at < 0
            ? (line, string.Empty)
            : (line[..at].Trim(), line[(at + 1)..].Trim());
    }

    /// <summary>
    /// 保存整张角色卡：角色名、服务器、每艘艇的名字一起提交。
    /// 任一名字不合法就退回去让人改，一个字节都不写。
    /// </summary>
    public async Task SaveAsync(CharacterRowViewModel row)
    {
        try
        {
            await roster.SaveAsync(
                    row.Id,
                    row.EditName,
                    row.EditWorld,
                    [.. row.Submarines.Select(x => new RosterSubmarine(x.Id, x.EditName))])
                .ConfigureAwait(true);

            StatusMessage = Loc.Format("characters.rosterSaved", row.EditName.Trim());
            row.IsEditing = false;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (ValidationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    public async Task ToggleActiveAsync(CharacterRowViewModel row)
    {
        bool target = !row.IsActive;
        await characters.SetActiveAsync(row.Id, target).ConfigureAwait(true);
        StatusMessage = target ? Loc.Format("characters.enabledTo", row.DisplayName) : Loc.Format("characters.disabledTo", row.DisplayName);
        await LoadAsync().ConfigureAwait(true);
    }

    public async Task DeleteAsync(CharacterRowViewModel row)
    {
        await characters.DeleteAsync(row.Id).ConfigureAwait(true);
        StatusMessage = Loc.Format("characters.deleted", row.DisplayName);
        await LoadAsync().ConfigureAwait(true);
    }
}
