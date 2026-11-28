using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Errors;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

public sealed partial class CharacterManagerViewModel(
    CharacterService characters,
    SubmarineService submarines,
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
        Rows = new ObservableCollection<CharacterRowViewModel>(
            all.Select(x => new CharacterRowViewModel(x, this)));
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

    public async Task RenameAsync(CharacterRowViewModel row, string name, string world)
    {
        try
        {
            await characters.RenameAsync(row.Id, name, world).ConfigureAwait(true);
            row.IsEditing = false;
            StatusMessage = Loc.Format("characters.renamed", name.Trim());
            await LoadAsync().ConfigureAwait(true);
        }
        catch (ValidationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    /// <summary>展开就打这个角色名下的艇拉一次；收起什么都不做。</summary>
    public async Task ToggleSubmarinesAsync(CharacterRowViewModel row)
    {
        row.IsEditing = false;
        row.IsExpanded = !row.IsExpanded;

        if (!row.IsExpanded)
            return;

        IReadOnlyList<Submarine> all = await submarines.ListAsync(row.Id).ConfigureAwait(true);
        row.SetSubmarines(all);
    }

    public async Task RenameSubmarineAsync(SubmarineRowViewModel row)
    {
        try
        {
            await submarines.RenameAsync(row.Id, row.CharacterId, row.EditName).ConfigureAwait(true);
            StatusMessage = Loc.Format("characters.submarineRenamed", row.EditName.Trim());

            // 名字改了要重拉：行里的 model 还是旧名字，不重建就对不上了。
            if (Rows.FirstOrDefault(x => x.Id == row.CharacterId) is { } owner)
            {
                IReadOnlyList<Submarine> all = await submarines.ListAsync(owner.Id).ConfigureAwait(true);
                owner.SetSubmarines(all);
            }
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
