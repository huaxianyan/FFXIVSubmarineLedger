using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>角色列表里的一行：改名字、停用、删除，以及展开看这个角色名下的艇。</summary>
public sealed partial class CharacterRowViewModel(Character model, CharacterManagerViewModel owner) : ObservableObject
{
    public Guid Id => model.Id;

    public string Name => model.Name;

    public string DisplayName => model.DisplayName;

    public string ModelWorld => model.World;

    public string WorldLabel => string.IsNullOrWhiteSpace(model.World) ? Loc.Get("characters.worldMissing") : model.World;

    public bool IsActive => model.IsActive;

    public bool IsInactive => !model.IsActive;

    public string ToggleLabel => model.IsActive ? Loc.Get("characters.disable") : Loc.Get("characters.enable");

    /// <summary>正在改名。改的时候整张卡片切到两个输入框。</summary>
    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = model.Name;

    [ObservableProperty]
    private string _editWorld = model.World;

    /// <summary>艇列表展开了没有。默认不展开：多数时候只想看角色。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandGlyph))]
    [NotifyPropertyChangedFor(nameof(ShowSubmarineList))]
    private bool _isExpanded;

    /// <summary>这个角色名下的艇。展开时懒加载。</summary>
    public ObservableCollection<SubmarineRowViewModel> Submarines { get; } = [];

    public bool HasSubmarines => Submarines.Count > 0;

    public bool HasNoSubmarines => Submarines.Count == 0;

    /// <summary>展开了才显示艇那块（含「还没记录到艇」的提示）。</summary>
    public bool ShowSubmarineList => IsExpanded;

    /// <summary>「潜水艦 4 艘」，折叠时也看得到有几艘。</summary>
    public string SubmarineCountLabel => Loc.Format("characters.submarineCount", Submarines.Count);

    public string ExpandGlyph => IsExpanded ? "▾" : "▸";

    public void Relocalize()
    {
        OnPropertyChanged(nameof(WorldLabel));
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(SubmarineCountLabel));
    }

    /// <summary>换一批艇（展开、改名、落库后刷新都用这个）。</summary>
    public void SetSubmarines(IReadOnlyList<Submarine> submarines)
    {
        Submarines.Clear();

        foreach (Submarine submarine in submarines)
            Submarines.Add(new SubmarineRowViewModel(submarine, owner));

        OnPropertyChanged(nameof(HasSubmarines));
        OnPropertyChanged(nameof(HasNoSubmarines));
        OnPropertyChanged(nameof(SubmarineCountLabel));
        OnPropertyChanged(nameof(ShowSubmarineList));
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditName = model.Name;
        EditWorld = model.World;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private Task SaveEdit() => owner.RenameAsync(this, EditName, EditWorld);

    [RelayCommand]
    private Task ToggleActive() => owner.ToggleActiveAsync(this);

    [RelayCommand]
    private Task ToggleSubmarines() => owner.ToggleSubmarinesAsync(this);

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}

/// <summary>角色下面的一艘艇。游戏里改过名之后在这里改一次，之后识别到新名字还是这一艘。</summary>
public sealed partial class SubmarineRowViewModel(Submarine model, CharacterManagerViewModel owner) : ObservableObject
{
    public Guid Id => model.Id;

    public Guid CharacterId => model.CharacterId;

    public string Name => model.Name;

    [ObservableProperty]
    private string _editName = model.Name;

    /// <summary>名字没变就没必要写库。</summary>
    public bool CanSave => !string.Equals(EditName?.Trim(), Name, StringComparison.Ordinal);

    partial void OnEditNameChanged(string value) => OnPropertyChanged(nameof(CanSave));

    [RelayCommand]
    private Task Save() => owner.RenameSubmarineAsync(this);
}
