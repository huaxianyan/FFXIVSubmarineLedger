using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Application.Abstractions;
using FFXIVSubLedger.Application.Records;
using FFXIVSubLedger.Domain.Characters;
using FFXIVSubLedger.Desktop.Services;
using FFXIVSubLedger.Infrastructure.Catalog;
using FFXIVSubLedger.Infrastructure.Parsing;
using FFXIVSubLedger.Platform.Windows.GameChat;

namespace FFXIVSubLedger.Desktop.ViewModels;

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

    /// <summary>已处理过的聊天行，用于去重（同一句话可能合法地出现多次）。</summary>
    private readonly LineDeduplicator _deduplicator = new();

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

    public string WatchButtonLabel => IsWatching ? "停止监听" : "自动监听";

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
            return target is null ? "还没有角色，先到「角色管理」添加一个" : target.DisplayName;
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
            StatusMessage = "读不到系统剪贴板。";
            return;
        }

        try
        {
            // 截图工具一般直接放位图；从资源管理器复制的则是图片文件。
            Bitmap? bitmap = await clipboard.TryGetBitmapAsync().ConfigureAwait(true);
            if (bitmap is not null)
            {
                Accept(bitmap, "剪贴板");
                return;
            }

            IReadOnlyList<IStorageItem>? items = await clipboard.TryGetFilesAsync().ConfigureAwait(true);
            if (items is { Count: > 0 } && items[0] is IStorageFile file)
            {
                await LoadFileAsync(file, "剪贴板文件").ConfigureAwait(true);
                return;
            }

            StatusMessage = "剪贴板里没有图片。先截好图再试一次。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"读剪贴板失败：{exception.Message}";
        }
    }

    /// <summary>拖放落点。</summary>
    public async Task AcceptDroppedAsync(IDataTransfer? transfer)
    {
        if (transfer is null)
        {
            StatusMessage = "没读到拖进来的内容。";
            return;
        }

        try
        {
            Bitmap? bitmap = transfer.TryGetBitmap();
            if (bitmap is not null)
            {
                Accept(bitmap, "拖入");
                return;
            }

            if (transfer.TryGetFile() is IStorageFile file)
            {
                await LoadFileAsync(file, "拖入文件").ConfigureAwait(true);
                return;
            }

            StatusMessage = "拖进来的内容里没有图片。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"读取拖入内容失败：{exception.Message}";
        }
    }

    private async Task PickFileAsync()
    {
        if (_topLevelProvider() is not { } topLevel)
        {
            StatusMessage = "打不开文件选择器。";
            return;
        }

        try
        {
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "选择收艇截图",
                    AllowMultiple = false,
                    FileTypeFilter = [FilePickerFileTypes.ImageAll],
                }).ConfigureAwait(true);

            if (files.Count == 0)
                return;

            await LoadFileAsync(files[0], "图片文件").ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusMessage = $"选择文件失败：{exception.Message}";
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
            Accept(new Bitmap(path), "启动参数指定");
        }
        catch (Exception exception)
        {
            StatusMessage = $"打不开这个文件：{exception.Message}";
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
            StatusMessage = $"打不开这个文件：{exception.Message}";
        }
    }

    private void Accept(Bitmap bitmap, string source)
    {
        // 旧位图不主动 Dispose：合成器可能还持有它，交给 GC 更稳妥，单张也只有几百 KB。
        Source = bitmap;
        SourceLabel = source;
        ImageInfo = string.Create(
            CultureInfo.InvariantCulture,
            $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height} 像素");

        ScreenshotAnalyzer.AnalyzedScreenshot analyzed = ScreenshotAnalyzer.Analyze(bitmap);
        Overlay = analyzed.Overlay;

        if (analyzed.Lines.Count == 0)
        {
            LineSummary = string.Empty;
            LineHint = string.Empty;
            StatusMessage = "没找到文本行。截图里至少要有一段看得清的字。";
            return;
        }

        int[] heights = [.. analyzed.Lines.Select(x => x.Height)];
        LineSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"切出 {analyzed.Lines.Count} 行 · 行高 {heights.Min()}–{heights.Max()} px · 起始行 {analyzed.Lines[0].Top} px");

        LineHint = $"物名与数量还没核对（下一步接行模板与词表）。先看行框有没有切半或者漏行。";
        StatusMessage = "行已切出，正在认字…";

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
            OcrSummary = string.Create(CultureInfo.InvariantCulture, $"读出 {texts.Count} 行文字");

            IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(texts, _catalog.Value);

            ReplaceRows(loot);

            StatusMessage = loot.Count == 0
                ? "没认出入手行。这段日志里可能没有收艇记录。"
                : "逐行核对数量与品质，确认后写账。";
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException)
        {
            if (generation != _recognizeGeneration)
                return;

            OcrSummary = string.Empty;
            StatusMessage = $"日文 OCR 模型缺失，无法认字：{exception.Message}";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (generation != _recognizeGeneration)
                return;

            OcrSummary = string.Empty;
            StatusMessage = $"文字识别失败：{exception.Message}";
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
        StatusMessage = "正在监听聊天框…";

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
                    MergeChatLines(lines);

                await Task.Delay(WatchInterval, token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // 用户点了停止，正常退出。
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"监听中断：{exception.Message}";
        }
        finally
        {
            _watchCancellation?.Dispose();
            _watchCancellation = null;
            IsWatching = false;
            StatusMessage = StatusMessage.StartsWith("正在监听", StringComparison.Ordinal)
                ? "已停止监听。"
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
    private int MergeChatLines(IReadOnlyList<string> lines)
    {
        IReadOnlyList<string> fresh = _deduplicator.TakeNew(lines);
        if (fresh.Count == 0)
            return 0;

        IReadOnlyList<RecognizedLoot> loot = LootScanner.Scan(fresh, _catalog.Value);
        if (loot.Count == 0)
            return 0;

        // 只追加，不清空：用户可能已经改过前面几行的数量或品质。
        foreach (RecognizedLoot entry in loot)
        {
            LootRowViewModel row = new(entry);
            row.PropertyChanged += OnLootRowChanged;
            LootRows.Add(row);
        }

        RefreshLootTotals();
        return loot.Count;
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
                StatusMessage = "聊天框里没有新内容。先收艇，让入手记录出现在聊天框里再读。";
                return;
            }

            OcrText = string.Join(Environment.NewLine, lines);
            OcrSummary = string.Create(CultureInfo.InvariantCulture, $"读到 {lines.Count} 行聊天内容");

            int added = MergeChatLines(lines);
            StatusMessage = added == 0
                ? "聊天内容里没有新的入手记录。"
                : "逐行核对数量与品质，确认后写账。";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"读聊天框失败：{exception.Message}";
        }
        finally
        {
            IsRecognizing = false;
        }
    }

    private void ReplaceRows(IReadOnlyList<RecognizedLoot> loot)
    {
        LootRows.Clear();
        foreach (RecognizedLoot entry in loot)
        {
            LootRowViewModel row = new(entry);
            row.PropertyChanged += OnLootRowChanged;
            LootRows.Add(row);
        }

        RefreshLootTotals();
    }

    private void OnLootRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshLootTotals();

    private void RefreshLootTotals()
    {
        List<LootRowViewModel> included =
        [
            .. LootRows.Where(x => x.IsIncluded && x.EffectedQuantity > 0),
        ];

        int total = included.Sum(x => x.Subtotal);
        int unknown = included.Count(x => x.IsUnknown && x.UnitPrice == 0);

        LootSummary = LootRows.Count == 0
            ? string.Empty
            : included.Count == 0
                ? "没有勾选任何一行。"
                : string.Create(CultureInfo.InvariantCulture, $"本次计入 {included.Count} 行 · 合计 {total:N0} 吉尔");

        if (LootSummary.Length > 0 && unknown > 0)
            LootSummary += string.Create(CultureInfo.InvariantCulture, $"，还有 {unknown} 行没填单价");

        OnPropertyChanged(nameof(HasLootRows));
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
            StatusMessage = "还没有角色。先到「角色管理」里添加一个，再记账。";
            return;
        }

        List<LootRowViewModel> included =
        [
            .. LootRows.Where(x => x.IsIncluded && x.EffectedQuantity > 0 && x.UnitPrice > 0),
        ];

        if (included.Count == 0)
        {
            StatusMessage = "没有可入账的行。";
            return;
        }

        DateOnly date = DateOnly.FromDateTime(DateTime.Now);

        try
        {
            int records = 0;
            int total = 0;

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

            string summary = string.Create(
                CultureInfo.InvariantCulture,
                $"已记入「{target.DisplayName}」：{date:yyyy-MM-dd} · {records} 笔（按艇分开）· 合计 {total:N0} 吉尔");

            Clear();
            StatusMessage = summary;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusMessage = $"写账失败：{exception.Message}";
        }
    }
}
