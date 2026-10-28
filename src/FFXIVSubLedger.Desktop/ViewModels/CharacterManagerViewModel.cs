using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Application.Characters;
using FFXIVSubLedger.Application.Errors;
using FFXIVSubLedger.Domain.Characters;

namespace FFXIVSubLedger.Desktop.ViewModels;

public sealed partial class CharacterManagerViewModel(CharacterService characters) : ObservableObject
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

    public async Task SetDefaultAsync(CharacterRowViewModel row)
    {
        await characters.SetDefaultAsync(row.Id).ConfigureAwait(true);
        StatusMessage = Loc.Format("characters.defaultSet", row.DisplayName);
        await LoadAsync().ConfigureAwait(true);
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
