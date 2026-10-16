using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        CountLabel = Rows.Count == 0 ? string.Empty : $"共 {Rows.Count} 个角色";
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        try
        {
            await characters.AddAsync(NewName, NewWorld).ConfigureAwait(true);
            NewName = string.Empty;
            NewWorld = string.Empty;
            StatusMessage = "已添加角色。";
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
        StatusMessage = $"已将「{row.DisplayName}」设为默认角色。";
        await LoadAsync().ConfigureAwait(true);
    }

    public async Task ToggleActiveAsync(CharacterRowViewModel row)
    {
        bool target = !row.IsActive;
        await characters.SetActiveAsync(row.Id, target).ConfigureAwait(true);
        StatusMessage = target ? $"已启用「{row.DisplayName}」。" : $"已停用「{row.DisplayName}」。";
        await LoadAsync().ConfigureAwait(true);
    }

    public async Task DeleteAsync(CharacterRowViewModel row)
    {
        await characters.DeleteAsync(row.Id).ConfigureAwait(true);
        StatusMessage = $"已删除「{row.DisplayName}」。";
        await LoadAsync().ConfigureAwait(true);
    }
}
