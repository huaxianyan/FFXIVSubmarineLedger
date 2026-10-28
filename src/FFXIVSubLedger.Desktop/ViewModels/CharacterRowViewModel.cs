using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Domain.Characters;

namespace FFXIVSubLedger.Desktop.ViewModels;

public sealed partial class CharacterRowViewModel(Character model, CharacterManagerViewModel owner) : ObservableObject
{
    public Guid Id => model.Id;

    public string Name => model.Name;

    public string DisplayName => model.DisplayName;

    public string WorldLabel => string.IsNullOrWhiteSpace(model.World) ? Loc.Get("characters.worldMissing") : model.World;

    public bool IsActive => model.IsActive;

    public bool IsInactive => !model.IsActive;

    public bool IsDefault => model.IsDefault;

    public bool CanSetDefault => !model.IsDefault;

    public string ToggleLabel => model.IsActive ? Loc.Get("characters.disable") : Loc.Get("characters.enable");

    [RelayCommand]
    private Task SetDefault() => owner.SetDefaultAsync(this);

    [RelayCommand]
    private Task ToggleActive() => owner.ToggleActiveAsync(this);

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}
