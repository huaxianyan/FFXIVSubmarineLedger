using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Localization;
using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Desktop.Services;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;
using FFXIVSubmarineLedger.Platform.Windows.GameChat;

namespace FFXIVSubmarineLedger.Desktop.ViewModels;

/// <summary>
/// 收艇记账页。把截图弄进来、切行、认字、对词表、算出本次收益，
/// 由用户逐行核对后写入记录。
/// </summary>
public sealed partial class RecordsPageViewModel : ObservableObject
{
    private readonly Func<TopLevel?> _topLevelProvider;
    private readonly ILootRecordRepository _records;
    private readonly ISubmarineRepository _submarines;
    private readonly Lazy<ItemCatalog> _catalog;
    private readonly OcrService _ocr = new();
    private readonly SharlayanChatReader _chat = new();

    /// <summary>每载入一张图就加一，识别完发现代数变了就丢弃结果（用户可能已换成另一张）。</summary>
    private int _recognizeGeneration;

    /// <summary>已落成记录的入手行，避免同一批历史被重复加入。</summary>
    private readonly LootAccumulator _accumulator = new();

    private CancellationTokenSource? _watchCancellation;

    /// <summary>自动监听的轮询间隔。3 秒足够跟上收艇节奏，也不会把 CPU 拉起来。</summary>
    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(3);

    public RecordsPageViewModel(
        Func<TopLevel?> topLevelProvider,
        CharacterManagerViewModel characters,
        ILootRecordRepository records,
        ISubmarineRepository submarines,
        Lazy<ItemCatalog> catalog)
    {
        _topLevelProvider = topLevelProvider;
        _records = records;
        _submarines = submarines;
        _catalog = catalog;
        Characters = characters;

        Characters.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CharacterManagerViewModel.Rows))
                OnPropertyChanged(nameof(TargetCharacterLabel));
        };
    }

    public CharacterManagerViewModel Characters { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(ShowIntakeGuides))]
    private Bitmap? _source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private Bitmap? _overlay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private bool _showLineOverlay = true;

    [ObservableProperty]
    private string _sourceLabel = string.Empty;

    [ObservableProperty]
    private string _imageInfo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLineSummary))]
    private string _lineSummary = string.Empty;

    [ObservableProperty]
    private string _lineHint = string.Empty;

    /// <summary>OCR 读到的逐行文字，只用于核对识别质量。</summary>
    [ObservableProperty]
    private string _ocrText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOcrSummary))]
    private string _ocrSummary = string.Empty;

    [ObservableProperty]
    private bool _isRecognizing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WatchButtonLabel))]
    private bool _isWatching;

    public string WatchButtonLabel => IsWatching ? Loc.Get("records.watchStop") : Loc.Get("records.watchStart");

    /// <summary>完整金额，够大时后面补一句紧凑写法。句子里的金额都走这里。</summary>
    private static string MoneyWithCompact(long value) =>
        value >= 10_000
            ? string.Create(CultureInfo.InvariantCulture, $"{GilFormat.Full(value)}（{GilFormat.Compact(value)}）")
            : GilFormat.Full(value);

    /// <summary>换语言后刷新页内由代码拼出来的文字。</summary>
    public void Relocalize()
    {
        OnPropertyChanged(nameof(WatchButtonLabel));

        foreach (LootRowViewModel row in LootRows)
            row.Relocalize();

        RefreshLootTotals();
    }

    /// <summary>认出的入手记录，逐行可改数量与品质，取消勾选就不入账。</summary>
    public ObservableCollection<LootRowViewModel> LootRows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLootSummary))]
    private string _lootSummary = string.Empty;

    /// <summary>拖拽悬停中，用于把拖放区高亮出来。</summary>
    [ObservableProperty]
    private bool _isDragOver;

    public bool HasImage => Source is not null;

    public bool HasLineSummary => LineSummary.Length > 0;

    public bool HasOcrSummary => OcrSummary.Length > 0;

    public bool HasLootSummary => LootSummary.Length > 0;

    public bool HasLootRows => LootRows.Count > 0;

    /// <summary>
    /// 底部三张引导卡只在「既没有图、也没有识别结果」时露出来。
    /// 直读聊天框拿到的结果同样不需要再看「怎么把图弄进来」。
    /// </summary>
    public bool ShowIntakeGuides => !HasImage && LootRows.Count == 0;

    public bool CanSave => LootRows.Any(x => x.IsIncluded && x.EffectedQuantity > 0 && x.UnitPrice > 0);

    /// <summary>预览图。勾上叠加就显示画了行框的版，否则显示原图。</summary>
    public Bitmap? Preview => ShowLineOverlay && Overlay is not null ? Overlay : Source;

    public bool HasStatus => StatusMessage.Length > 0;

    /// <summary>这笔收益会记到谁名下。</summary>
    public string TargetCharacterLabel
    {
        get
        {
            CharacterRowViewModel? target =
                Characters.Rows.FirstOrDefault(x => x.IsDefault) ?? Characters.Rows.FirstOrDefault();
            return target is null ? Loc.Get("status.noCharacterHint") : target.DisplayName;
        }
    }

    [RelayCommand]
    private Task ReadClipboard() => ReadFromClipboardAsync();

    [RelayCommand]
    private Task PickFile() => PickFileAsync();

    [RelayCommand]
    private void Clear()
    {
        Source = null;
        Overlay = null;
        SourceLabel = string.Empty;
        ImageInfo = string.Empty;
        StatusMessage = string.Empty;
        LineSummary = string.Empty;
        LineHint = string.Empty;
        OcrSummary = string.Empty;
        OcrText = string.Empty;
        LootSummary = string.Empty;
        LootRows.Clear();
        RefreshLootTotals();
    }

    /// <summary>读系统剪贴板。视图层的 Ctrl+V 也走这里。</summary>
    public async Task ReadFromClipboardAsync()
    {
        if (_topLevelProvider()?.Clipboard is not { } clipboard)
        {
            StatusMessage = Loc.Get("records.clipboardUnavailable");
            return;
        }

        try
        {
            // 截图工具一般直接放位图；从资源管理器复制的则是图片文件。
            Bitmap? bitmap = await clipboard.TryGetBitmapAsync().ConfigureAwait(true);
            if (bitmap is not null)
            {
                Accept(bitmap, Loc.Get("records.sourceClipboard"));
                return;
            }

            IReadOnlyList<IStorageItem>? items = await clipboard.TryGetFilesAsync().ConfigureAwait(true);
            if (items is { Count: > 0 } && items[0] is IStorageFile file)
            {
                await LoadFileAsync(file, Loc.Get("records.sourceClipboardFile")).ConfigureAwait(true);
                return;
            }

            StatusMessage = Loc.Get("records.clipboardNoImage");
        }
        catch (Exception exception)
        {
            StatusMessage = Loc.Format("records.clipboardFailed", exception.Message);
        }
    }

    /// <summary>拖放落点。</summary>
    public async Task AcceptDroppedAsync(IDataTransfer? transfer)
    {
        if (transfer is null)
        {
            StatusMessage = Loc.Get("records.dropEmpty");
            return;
        }

        try
        {
            Bitmap? bitmap = transfer.TryGetBitmap();
            if (bitmap is not null)
            {
                Accept(bitmap, Loc.Get("records.sourceDrop"));
                return;
            }

            if (transfer.TryGetFile() is IStorageFile file)
            {
                await LoadFileAsync(file, Loc.Get("records.sourceDropFile")).ConfigureAwait(true);
                return;
            }

            StatusMessage = Loc.Get("records.dropNoImage");
        }
        catch (Exception exception)
        {
            StatusMessage = Loc.Format("records.dropFailed", exception.Message);
        }
    }

    private async Task PickFileAsync()
    {
        if (_topLevelProvider() is not { } topLevel)
        {
            StatusMessage = Loc.Get("records.pickerUnavailable");
            return;
        }

        try
        {
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = Loc.Get("records.pickTitle"),
                    AllowMultiple = false,
                    FileTypeFilter = [FilePickerFileTypes.ImageAll],
                }).ConfigureAwait(true);

            if (files.Count == 0)
                return;

            await LoadFileAsync(files[0], Loc.Get("records.sourceFile")).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = Loc.Format("records.pickFailed", exception.Message);
        }
    }

    /// <summary>
    /// 从磁盘路径直接载入一张图。给开发期的 <c>--image</c> 启动参数用，
    /// 免得每次调检测都要手动拖一张进去；正常交互走拖放/剪贴板/选择器。
    /// </summary>
    public void LoadFromPath(string path)
    {
        try
        {
            Accept(new Bitmap(path), Loc.Get("records.sourceArgs"));
        }
        catch (Exception exception)
        {
            StatusMessage = Loc.Format("records.openFailed", exception.Message);
        }
    }

    private async Task LoadFileAsync(IStorageFile file, string source)
    {
        try
        {
            await using Stream stream = await file.OpenReadAsync().ConfigureAwait(true);
            Accept(new Bitmap(stream), source);
        }
        catch (Exception exception)
        {
            StatusMessage = Loc.Format("records.openFailed", exception.Message);
        }
    }

    private void Accept(Bitmap bitmap, string source)
    {
        // 旧位图不主动 Dispose：合成器可能还持有它，交给 GC 更稳妥，单张也只有几百 KB。
        Source = bitmap;
        SourceLabel = source;
        ImageInfo = Loc.Format("records.imageInfo", bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        ScreenshotAnalyzer.AnalyzedScreenshot analyzed = ScreenshotAnalyzer.Analyze(bitmap);
        Overlay = analyzed.Overlay;

        if (analyzed.Lines.Count == 0)
        {
            LineSummary = string.Empty;
            LineHint = string.Empty;
            StatusMessage = Loc.Get("records.linesNotFound");
            return;
        }

        int[] heights = [.. analyzed.Lines.Select(x => x.Height)];
        LineSummary = Loc.Format(
            "records.lineSummary",
            analyzed.Lines.Count,
            heights.Min(),
            heights.Max(),
            analyzed.Lines[0].Top);

        LineHint = Loc.Get("records.lineHint");
        StatusMessage = Loc.Get("records.linesCut");

        _ = RecognizeAsync(analyzed);
    }

    private async Task RecognizeAsync(ScreenshotAnalyzer.AnalyzedScreenshot analyzed)
    {
        int generation = ++_recognizeGeneration;

        IsRecognizing = true;
        OcrText = string.Empty;
        LootRows.Clear();
        RefreshLootTotals();

        try
        {
            IReadOnlyList<string> texts = await _ocr
                .ReadLinesAsync(analyzed.Gray, analyzed.Lines)
                .ConfigureAwait(true);

            if (generation != _recognizeGeneration)
                return;

            int index = 1;
            List<string> formatted =
            [
                .. texts.Select(text => string.Create(CultureInfo.InvariantCulture, $"#{index++:00}  {text}")),
            ];

            OcrText = string.Join(Environment.NewLine, formatted);
            OcrSummary = Loc.Format("records.ocrSummary", texts.Count);

            IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(texts, _catalog.Value);

            ReplaceRows(loot);

            StatusMessage = loot.Count == 0
                ? Loc.Get("records.noLoot")
                : Loc.Get("records.confirmHint");
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException)
        {
            if (generation != _recognizeGeneration)
                return;

            OcrSummary = string.Empty;
            StatusMessage = Loc.Format("records.ocrMissing", exception.Message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (generation != _recognizeGeneration)
                return;

            OcrSummary = string.Empty;
            StatusMessage = Loc.Format("records.ocrFailed", exception.Message);
        }
        finally
        {
            if (generation == _recognizeGeneration)
                IsRecognizing = false;
        }
    }

    /// <summary>
    /// 开关自动监听。打开后每 3 秒读一次新聊天，一旦出现入手行就自动加进本次账。
    ///
    /// 游戏里的聊天框是固定 1000 条的环形表，所以「先收完艇、再开程序」也能补读——
    /// 只要这中间没有刷出 1000 条新聊天把它顶掉。
    ///
    /// 命令本身是同步的：监听循环在后台跑，否则 async 命令执行期间按钮会被禁用，想停都停不了。
    /// </summary>
    [RelayCommand]
    private void ToggleWatch()
    {
        if (IsWatching)
        {
            _watchCancellation?.Cancel();
            return;
        }

        if (!_chat.TryAttach(out string message))
        {
            StatusMessage = message;
            return;
        }

        _watchCancellation = new CancellationTokenSource();
        IsWatching = true;
        StatusMessage = Loc.Get("records.watching");

        _ = WatchLoopAsync(_watchCancellation.Token);
    }

    private async Task WatchLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                IReadOnlyList<string> lines = await Task.Run(_chat.ReadNewLines, token).ConfigureAwait(true);

                if (lines.Count > 0)
                {
                    IReadOnlyList<RecognizedLoot> added = MergeChatLines(lines);
                    OcrSummary = Loc.Format("records.watchSummary", lines.Count);

                    if (added.Count > 0)
                    {
                        StatusMessage = Loc.Format("records.watchAdded", added.Count, LootRows.Count);
                    }
                }

                await Task.Delay(WatchInterval, token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户点了停止，正常退出。
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("records.watchInterrupted", exception.Message);
        }
        finally
        {
            _watchCancellation?.Dispose();
            _watchCancellation = null;
            IsWatching = false;
            StatusMessage = StatusMessage.StartsWith(Loc.Get("records.watching"), StringComparison.Ordinal)
                ? Loc.Get("records.watchStopped")
                : StatusMessage;
        }
    }

    /// <summary>
    /// 把新读到的聊天行并进本次账。
    ///
    /// 读取器每次调用都会把整段累计历史再给一遍（实测第二轮与首轮同为 624 行），
    /// 所以必须去重；去重按「同一句话的出现次数」算，因为同一种道具分两行入包时
    /// 日志里就是两条一模一样的行，第二条也是真收益。
    /// </summary>
    private IReadOnlyList<RecognizedLoot> MergeChatLines(IReadOnlyList<string> lines)
    {
        // 归属必须在**整段有序缓冲**上算：只看新出现的那几行，往往拿不到后面那行「探索完了」，
        // 归属就会从错误的上文里算出来（实测把 Cranberry 的一笔记到了 Blackberry）。
        // 还没等到艇名的行先不结算，等下一轮有了归属再算。
        IReadOnlyList<RecognizedLoot> all = LootScanner.Scan(lines, _catalog.Value, includeUnattributed: false);
        IReadOnlyList<RecognizedLoot> loot = _accumulator.TakeNew(all);
        if (loot.Count == 0)
            return [];

        IReadOnlyList<SubmarineChoice> choices = BuildSubmarineChoices(loot);

        // 只追加，不清空：用户可能已经改过前面几行的数量或品质。
        foreach (RecognizedLoot entry in loot)
        {
            LootRowViewModel row = new(entry, choices);
            row.PropertyChanged += OnLootRowChanged;
            LootRows.Add(row);
        }

        RefreshLootTotals();
        return loot;
    }

    /// <summary>
    /// 直接从游戏聊天框读一遍。读到的文本是游戏自己的渲染结果，
    /// 不会出现 OCR 那种认错字或漏掉 HQ 标记的问题；读不到时仍可退回截图识别。
    /// </summary>
    [RelayCommand]
    private async Task ReadChatLogAsync()
    {
        IsRecognizing = true;

        try
        {
            if (!_chat.TryAttach(out string message))
            {
                StatusMessage = message;
                return;
            }

            IReadOnlyList<string> lines = await Task.Run(_chat.ReadNewLines).ConfigureAwait(true);

            if (lines.Count == 0)
            {
                StatusMessage = Loc.Get("records.chatEmpty");
                return;
            }

            OcrText = string.Join(Environment.NewLine, lines);
            OcrSummary = Loc.Format("records.chatSummary", lines.Count);

            IReadOnlyList<RecognizedLoot> added = MergeChatLines(lines);
            int unknown = added.Count(x => !x.IsKnown);
            string unknownNote = unknown > 0 ? Loc.Format("records.chatUnknownNote", unknown) : string.Empty;

            StatusMessage = added.Count == 0
                ? Loc.Format("records.chatNoLoot", lines.Count)
                : Loc.Format("records.chatAdded", lines.Count, added.Count, unknownNote);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("records.chatFailed", exception.Message);
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private void ReplaceRows(IReadOnlyList<RecognizedLoot> loot)
    {
        IReadOnlyList<SubmarineChoice> choices = BuildSubmarineChoices(loot);

        LootRows.Clear();
        foreach (RecognizedLoot entry in loot)
        {
            LootRowViewModel row = new(entry, choices);
            row.PropertyChanged += OnLootRowChanged;
            LootRows.Add(row);
        }

        RefreshLootTotals();
    }

    /// <summary>
    /// 确认行里可选的艇：本批出现过的艇 + 「未标明艇」。
    /// 识别把两艘艇的收益串在一起时，用户能在这里改回去。
    /// </summary>
    private static IReadOnlyList<SubmarineChoice> BuildSubmarineChoices(IReadOnlyList<RecognizedLoot> loot)
    {
        List<SubmarineChoice> choices = [new(string.Empty)];

        choices.AddRange(loot
            .Select(x => x.SubmarineName)
            .Where(name => name is { Length: > 0 })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => new SubmarineChoice(name!)));

        return choices;
    }

    private void OnLootRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshLootTotals();

    private void RefreshLootTotals()
    {
        List<LootRowViewModel> included =
        [
            .. LootRows.Where(x => x.IsIncluded && x.EffectedQuantity > 0),
        ];

        long total = included.Sum(x => x.Subtotal);
        int unknown = included.Count(x => x.IsUnknown && x.UnitPrice == 0);

        LootSummary = LootRows.Count == 0
            ? string.Empty
            : included.Count == 0
                ? Loc.Get("records.summaryNone")
                : Loc.Format("records.summaryTotal", included.Count, MoneyWithCompact(total));

        if (LootSummary.Length > 0 && unknown > 0)
            LootSummary += Loc.Format("records.summaryUnpriced", unknown);

        OnPropertyChanged(nameof(HasLootRows));
        OnPropertyChanged(nameof(ShowIntakeGuides));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 把勾选的行写成一笔记录。归属角色取默认角色，没有默认就取第一个。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        CharacterRowViewModel? target =
            Characters.Rows.FirstOrDefault(x => x.IsDefault) ?? Characters.Rows.FirstOrDefault();

        if (target is null)
        {
            StatusMessage = Loc.Get("records.noCharacter");
            return;
        }

        List<LootRowViewModel> selected =
        [
            .. LootRows.Where(x => x.IsIncluded && x.EffectedQuantity > 0),
        ];

        if (selected.Count == 0)
        {
            StatusMessage = Loc.Get("records.nothingToSave");
            return;
        }

        // 词表外的物名没填单价就不许写账：静默跳过等于把真收益算丢。
        int unpriced = selected.Count(x => x.UnitPrice <= 0);
        if (unpriced > 0)
        {
            StatusMessage = Loc.Format("records.unpricedBlocked", unpriced);
            return;
        }

        List<LootRowViewModel> included = selected;

        DateOnly date = DateOnly.FromDateTime(DateTime.Now);

        try
        {
            int records = 0;
            long total = 0;

            // 一艘艇一笔记录：艇名在日志里是分组凭据，混在一笔里就没法按艇统计了。
            foreach (IGrouping<string?, LootRowViewModel> group in included.GroupBy(x => x.SubmarineName))
            {
                Guid? submarineId = null;

                if (group.Key is { Length: > 0 } name)
                {
                    Submarine? submarine = await _submarines.FindByNameAsync(target.Id, name).ConfigureAwait(true);
                    if (submarine is null)
                    {
                        submarine = new Submarine { CharacterId = target.Id, Name = name };
                        await _submarines.AddAsync(submarine).ConfigureAwait(true);
                    }

                    submarineId = submarine.Id;
                }

                Domain.Records.LootRecord record = LootRecordFactory.Build(
                    target.Id,
                    date,
                    [.. group.Select(x => x.ToEntry())]);

                record.SubmarineId = submarineId;
                await _records.AddAsync(record).ConfigureAwait(true);

                records++;
                total += record.TotalValue;
            }

            string summary = Loc.Format(
                "records.saved",
                target.DisplayName,
                $"{date:yyyy-MM-dd}",
                records,
                MoneyWithCompact(total));

            Clear();
            StatusMessage = summary;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = Loc.Format("records.saveFailed", exception.Message);
        }
    }
}
