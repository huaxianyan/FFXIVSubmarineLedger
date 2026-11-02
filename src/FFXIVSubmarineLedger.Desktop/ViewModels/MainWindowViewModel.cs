using FFXIVSubmarineLedger.Domain.Settings;
using FFXIVSubmarineLedger.Application.Updates;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Desktop.Services;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ApplicationComposition _composition;
    private readonly ChatLogDump _chatDump;

    public MainWindowViewModel(ApplicationComposition composition, Func<TopLevel?> topLevelProvider)
    {
        _composition = composition;
        _chatDump = new ChatLogDump(composition.DataDirectory);

        Characters = new CharacterManagerViewModel(composition.Characters);
        Records = new RecordsPageViewModel(
            topLevelProvider,
            Characters,
            composition.Records,
            composition.Submarines,
            composition.CatalogSource,
            _chatDump);
        Ledger = new LedgerPageViewModel(
            composition.Records,
            composition.Submarines,
            composition.Characters,
            topLevelProvider,
            composition.CatalogSource);
        Statistics = new StatisticsPageViewModel(
            composition.Records,
            composition.Submarines,
            composition.Characters,
            topLevelProvider,
            composition.CatalogSource);
        Settings = new SettingsPageViewModel(
            topLevelProvider,
            composition.Backup,
            composition.Settings,
            composition.Updates,
            composition.DataDirectory,
            composition.Database.DatabasePath,
            composition.CatalogSource,
            _chatDump);

        // 换语言后页头、页标签与统计里的文案要跟着变。
        _ = CheckForUpdatesOnStartupAsync();

        Loc.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageSubtitle));

            Records.Relocalize();
            Characters.Relocalize();
            Settings.Relocalize();
            Ledger.Relocalize();
            Statistics.Relocalize();

            StatusMessage = Characters.Rows.Count == 0
                ? Loc.Get("status.noCharacterHint")
                : Loc.Format("status.loadedCharacters", Characters.Rows.Count);
        };
    }

    public CharacterManagerViewModel Characters { get; }

    public RecordsPageViewModel Records { get; }

    public LedgerPageViewModel Ledger { get; }

    public StatisticsPageViewModel Statistics { get; }

    public SettingsPageViewModel Settings { get; }

    /// <summary>启动后自动查一次更新；查不到就安静地算了，不打扰记账。</summary>
    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            AppSettings options = await _composition.Settings.GetAsync().ConfigureAwait(true);
            if (!options.CheckForUpdates)
                return;

            UpdateCheckResult result = await _composition.Updates.CheckAsync().ConfigureAwait(true);

            if (result is { Status: UpdateStatus.Available, Release: { } release })
                StatusMessage = Loc.Format("status.updateAvailable", release.Version);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 更新检查失败不该占据状态栏：用户没要求这件事。
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecordsPage))]
    [NotifyPropertyChangedFor(nameof(IsLedgerPage))]
    [NotifyPropertyChangedFor(nameof(IsFilterPage))]
    [NotifyPropertyChangedFor(nameof(ActiveFilter))]
    [NotifyPropertyChangedFor(nameof(IsStatisticsPage))]
    [NotifyPropertyChangedFor(nameof(IsCharactersPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(PageSubtitle))]
    private NavigationPage _currentPage = NavigationPage.Records;

    [ObservableProperty]
    private string _statusMessage = Loc.Get("status.ready");

    [ObservableProperty]
    private bool _isBusy;

    public bool IsRecordsPage => CurrentPage == NavigationPage.Records;

    public bool IsLedgerPage => CurrentPage == NavigationPage.Ledger;

    /// <summary>标题栏的筛选只在这两页有意义。</summary>
    public bool IsFilterPage => CurrentPage is NavigationPage.Ledger or NavigationPage.Statistics;

    /// <summary>当前页那套筛选条件，标题栏绑它。</summary>
    public RecordFilter ActiveFilter => CurrentPage == NavigationPage.Statistics
        ? Statistics.Filter
        : Ledger.Filter;

    public bool IsStatisticsPage => CurrentPage == NavigationPage.Statistics;

    public bool IsCharactersPage => CurrentPage == NavigationPage.Characters;

    public bool IsSettingsPage => CurrentPage == NavigationPage.Settings;

    public string PageTitle => CurrentPage switch
    {
        NavigationPage.Records => Loc.Get("nav.records"),
        NavigationPage.Ledger => Loc.Get("nav.ledger"),
        NavigationPage.Statistics => Loc.Get("nav.statistics"),
        NavigationPage.Characters => Loc.Get("nav.characters"),
        NavigationPage.Settings => Loc.Get("nav.settings"),
        _ => Loc.Get("app.brand"),
    };

    public string PageSubtitle => CurrentPage switch
    {
        NavigationPage.Records => Loc.Get("page.records.subtitle"),
        NavigationPage.Ledger => Loc.Get("page.ledger.subtitle"),
        NavigationPage.Statistics => Loc.Get("page.statistics.subtitle"),
        NavigationPage.Characters => Loc.Get("page.characters.subtitle"),
        NavigationPage.Settings => Loc.Get("page.settings.subtitle"),
        _ => string.Empty,
    };

    [RelayCommand]
    private void ShowRecords() => CurrentPage = NavigationPage.Records;

    [RelayCommand]
    private void ShowLedger()
    {
        CurrentPage = NavigationPage.Ledger;
        _ = Ledger.LoadAsync();
    }

    [RelayCommand]
    private void ShowStatistics()
    {
        CurrentPage = NavigationPage.Statistics;
        _ = Statistics.LoadAsync();
    }

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
            await Ledger.LoadAsync().ConfigureAwait(true);
            await Statistics.LoadAsync().ConfigureAwait(true);
            StatusMessage = Characters.Rows.Count == 0
                ? Loc.Get("status.noCharacterHint")
                : Loc.Format("status.loadedCharacters", Characters.Rows.Count);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InitializeAsync()
    {
        // 先把调试开关读进来：用户可能在设置页里开过，重启后开监听就该继续记。
        AppSettings settings = await _composition.Settings.GetAsync().ConfigureAwait(true);
        _chatDump.Enabled = settings.DumpChatLog;

        await RefreshAsync().ConfigureAwait(true);
    }
}
