using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFXIVSubLedger.Desktop.Services;
using FFXIVSubLedger.Infrastructure.Recognition;

namespace FFXIVSubLedger.Desktop.ViewModels;

/// <summary>
/// 收艇记账页。负责把截图弄进来——拖入、Ctrl+V、读剪贴板、选文件——
/// 然后跑网格检测，把切分结果叠加回图上给用户核对。
/// 图标识别与计价属于 M3，还没接。
/// </summary>
public sealed partial class RecordsPageViewModel : ObservableObject
{
    private readonly Func<TopLevel?> _topLevelProvider;

    public RecordsPageViewModel(Func<TopLevel?> topLevelProvider, CharacterManagerViewModel characters)
    {
        _topLevelProvider = topLevelProvider;
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
    private bool _showOverlay = true;

    [ObservableProperty]
    private string _sourceLabel = string.Empty;

    [ObservableProperty]
    private string _imageInfo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetection))]
    private string _detectionSummary = string.Empty;

    [ObservableProperty]
    private string _detectionHint = string.Empty;

    /// <summary>拖拽悬停中，用于把拖放区高亮出来。</summary>
    [ObservableProperty]
    private bool _isDragOver;

    public bool HasImage => Source is not null;

    public bool HasStatus => StatusMessage.Length > 0;

    public bool HasDetection => DetectionSummary.Length > 0;

    /// <summary>预览图。勾上叠加就显示网格版，否则显示原图。</summary>
    public Bitmap? Preview => ShowOverlay && Overlay is not null ? Overlay : Source;

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
        DetectionSummary = string.Empty;
        DetectionHint = string.Empty;
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

        if (analyzed.Grid is { } grid)
        {
            DetectionSummary = string.Create(
                CultureInfo.InvariantCulture,
                $"格子边长 {grid.CellSize} px · {grid.Columns} 列 × {grid.Rows} 行 · " +
                $"{grid.OccupiedCount} 格有道具 / {grid.EmptyCount} 格空位");

            string numbers = string.Create(
                CultureInfo.InvariantCulture,
                $"列向相关度 {grid.ColumnCorrelation:F2}，相位锐度 {grid.PhaseSharpness:F2}");

            DetectionHint = grid.IsConfident
                ? $"{numbers}。对齐可信。"
                : $"{numbers}。偏低，请核对网格线是否贴合格子边缘。";

            StatusMessage = "网格已切分。图标识别还没接上（M3），先核对格子对不对。";
        }
        else
        {
            DetectionSummary = string.Empty;
            DetectionHint = string.Empty;
            StatusMessage = "没检测到网格。截图里至少要包含两格完整的背包格位。";
        }
    }
}
