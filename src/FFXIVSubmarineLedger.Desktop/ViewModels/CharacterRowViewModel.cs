using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Domain.Characters;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 角色列表里的一行：名字、服务器、名下的艇。点「编辑」就把这三样一起变成可改的，
/// 保存时一口气提交——艇名和角色名本来就是同一张卡上的东西。
/// </summary>
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

    /// <summary>正在编辑。改的时候整张卡片切到输入框，艇名一起改、一起保存。</summary>
    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editName = model.Name;

    [ObservableProperty]
    private string _editWorld = model.World;

    /// <summary>这个角色名下的艇，横排显示在卡片下方。</summary>
    public ObservableCollection<SubmarineRowViewModel> Submarines { get; } = [];

    /// <summary>没有艇时整块不显示——不写「还没有潜水艇」这类空话。</summary>
    public bool HasSubmarines => Submarines.Count > 0;

    public void Relocalize() => OnPropertyChanged(nameof(WorldLabel));

    /// <summary>换一批艇（首次加载、保存后重拉都用这个）。</summary>
    public void SetSubmarines(IReadOnlyList<Submarine> submarines)
    {
        Submarines.Clear();

        foreach (Submarine submarine in submarines)
            Submarines.Add(new SubmarineRowViewModel(submarine));

        OnPropertyChanged(nameof(HasSubmarines));
    }

    /// <summary>进编辑态：名字与每艘艇都回到当前值，别留着上次没提交的输入。</summary>
    public void BeginEdit()
    {
        EditName = model.Name;
        EditWorld = model.World;

        foreach (SubmarineRowViewModel submarine in Submarines)
            submarine.Reset();

        IsEditing = true;
    }

    [RelayCommand]
    private void StartEdit() => BeginEdit();

    [RelayCommand]
    private void CancelEdit()
    {
        foreach (SubmarineRowViewModel submarine in Submarines)
            submarine.Reset();

        EditName = model.Name;
        EditWorld = model.World;
        IsEditing = false;
    }

    [RelayCommand]
    private Task SaveEdit() => owner.SaveAsync(this);

    [RelayCommand]
    private Task ToggleActive() => owner.ToggleActiveAsync(this);

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}

/// <summary>角色下面的一艘艇：名字来自日志，游戏里改名后在这里改一次。</summary>
public sealed partial class SubmarineRowViewModel(Submarine model) : ObservableObject
{
    public Guid Id => model.Id;

    public string Name => model.Name;

    [ObservableProperty]
    private string _editName = model.Name;

    /// <summary>取消编辑或重新进入编辑时回到当前值。</summary>
    public void Reset() => EditName = model.Name;
}
