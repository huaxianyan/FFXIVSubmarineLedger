using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FFXIVSubLedger.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ApplicationComposition composition, Func<TopLevel?> topLevelProvider)
    {
        Characters = new CharacterManagerViewModel(composition.Characters);
        Records = new RecordsPageViewModel(topLevelProvider, Characters);
        Statistics = RoadmapPageViewModel.CreateStatistics();
        Settings = RoadmapPageViewModel.CreateSettings();
    }

    public CharacterManagerViewModel Characters { get; }

    public RecordsPageViewModel Records { get; }

    public RoadmapPageViewModel Statistics { get; }

    public RoadmapPageViewModel Settings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordsPage))]
    [NotifyPropertyChangedFor(nameof(IsStatisticsPage))]
    [NotifyPropertyChangedFor(nameof(IsCharactersPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(PageSubtitle))]
    private NavigationPage _currentPage = NavigationPage.Records;

    [ObservableProperty]
    private string _statusMessage = "就绪。";

    [ObservableProperty]
    private bool _isBusy;

    public bool IsRecordsPage => CurrentPage == NavigationPage.Records;

    public bool IsStatisticsPage => CurrentPage == NavigationPage.Statistics;

    public bool IsCharactersPage => CurrentPage == NavigationPage.Characters;

    public bool IsSettingsPage => CurrentPage == NavigationPage.Settings;

    public string PageTitle => CurrentPage switch
    {
        NavigationPage.Records => "收艇记账",
        NavigationPage.Statistics => "收益统计",
        NavigationPage.Characters => "角色管理",
        NavigationPage.Settings => "设置",
        _ => "潜艇收益",
    };

    public string PageSubtitle => CurrentPage switch
    {
        NavigationPage.Records => "截图识别收艇掉落，计价入账。",
        NavigationPage.Statistics => "按日、月、年查看收益走势。",
        NavigationPage.Characters => "管理收益归属的角色与部队。",
        NavigationPage.Settings => "数据目录、识别参数与关于。",
        _ => string.Empty,
    };

    [RelayCommand]
    private void ShowRecords() => CurrentPage = NavigationPage.Records;

    [RelayCommand]
    private void ShowStatistics() => CurrentPage = NavigationPage.Statistics;

    [RelayCommand]
    private void ShowCharacters() => CurrentPage = NavigationPage.Characters;

    [RelayCommand]
    private void ShowSettings() => CurrentPage = NavigationPage.Settings;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await Characters.LoadAsync().ConfigureAwait(true);
            StatusMessage = Characters.Rows.Count == 0
                ? "还没有角色，先到「角色管理」添加一个。"
                : $"已载入 {Characters.Rows.Count} 个角色。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task InitializeAsync() => RefreshAsync();
}
