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
    /// <summary>游戏里一个角色最多 4 艘潜水艇。</summary>
    public const int SubmarineSlots = 4;

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

    /// <summary>这个角色名下的艇，横排显示在卡片下方（只显示已经有的）。</summary>
    public ObservableCollection<SubmarineRowViewModel> Submarines { get; } = [];

    /// <summary>
    /// 编辑态的位置：已有的艇 + 补足到 4 个的空位。
    /// 新角色一开始一艘都没有，也要能先把名字填进去，不用等第一次收艇。
    /// </summary>
    public ObservableCollection<SubmarineSlotViewModel> Slots { get; } = [];

    public void Relocalize()
    {
        OnPropertyChanged(nameof(WorldLabel));

        foreach (SubmarineSlotViewModel slot in Slots)
            slot.Relocalize();
    }

    /// <summary>换一批艇（首次加载、保存后重拉都用这个）。</summary>
    public void SetSubmarines(IReadOnlyList<Submarine> submarines)
    {
        Submarines.Clear();

        foreach (Submarine submarine in submarines)
            Submarines.Add(new SubmarineRowViewModel(submarine));
    }

    /// <summary>进编辑态：角色字段与艇名位置都回到当前值，别留着上次没提交的输入。</summary>
    public void BeginEdit()
    {
        EditName = model.Name;
        EditWorld = model.World;

        Slots.Clear();

        foreach (SubmarineRowViewModel submarine in Submarines)
            Slots.Add(new SubmarineSlotViewModel(submarine.Id, submarine.Name, Slots.Count + 1));

        for (int i = Slots.Count; i < SubmarineSlots; i++)
            Slots.Add(new SubmarineSlotViewModel(null, string.Empty, i + 1));

        IsEditing = true;
    }

    [RelayCommand]
    private void StartEdit() => BeginEdit();

    [RelayCommand]
    private void CancelEdit()
    {
        EditName = model.Name;
        EditWorld = model.World;
        Slots.Clear();
        IsEditing = false;
    }

    [RelayCommand]
    private Task SaveEdit() => owner.SaveAsync(this);

    [RelayCommand]
    private Task ToggleActive() => owner.ToggleActiveAsync(this);

    [RelayCommand]
    private Task Delete() => owner.DeleteAsync(this);
}

/// <summary>角色下面的一艘艇（展示用）：名字来自日志或手填，游戏里改名后在这里改一次。</summary>
public sealed partial class SubmarineRowViewModel(Submarine model) : ObservableObject
{
    public Guid Id => model.Id;

    public string Name => model.Name;
}

/// <summary>
/// 编辑态的一个艇名位置。<see cref="Id"/> 是空的就是还没落库的新位置——
/// 填了名字保存时新建，空着就当没填。
/// </summary>
public sealed partial class SubmarineSlotViewModel : ObservableObject
{
    private readonly string _name;

    public SubmarineSlotViewModel(Guid? id, string name, int number)
    {
        Id = id;
        Number = number;
        _name = name;
        _editName = name;
    }

    public Guid? Id { get; }

    /// <summary>第几个位置，从 1 开始，只用来区分占位提示。</summary>
    public int Number { get; }

    /// <summary>已经有这艘艇时它的当前名字，新位置为空。</summary>
    public string Name => _name;

    [ObservableProperty]
    private string _editName;

    public string Placeholder => Loc.Format("characters.submarinePlaceholder", Number);

    public void Relocalize() => OnPropertyChanged(nameof(Placeholder));
}
