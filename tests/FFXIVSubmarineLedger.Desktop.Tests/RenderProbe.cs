using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Catalog;
using FFXIVSubmarineLedger.Infrastructure.Parsing;
using FFXIVSubmarineLedger.Desktop.Services;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 把真实视图渲染成 PNG 存盘，供人工核对排版。
/// 无头渲染不开窗口、不抢焦点，不会打断用户正在做的事。
///
/// 只在设了 <c>PROBE_OUT</c> 时才干活：平时的测试跑不写文件。
/// </summary>
public sealed class RenderProbe
{
    [AvaloniaFact]
    public async Task RenderPages()
    {
        string? output = Environment.GetEnvironmentVariable("PROBE_OUT");
        if (string.IsNullOrWhiteSpace(output))
            return;

        string stem = Path.GetFileNameWithoutExtension(output);
        string directory = Path.GetDirectoryName(output)!;

        LedgerPageViewModel ledger = new(
            Sample.RecordRepository, Sample.SubmarineRepository, Sample.Characters, () => null,
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
        await ledger.LoadAsync();
        Render(new Views.LedgerPageView { DataContext = ledger }, 1200, 700,
            Path.Combine(directory, $"{stem}-ledger.png"));

        StatisticsPageViewModel statistics = new(
            Sample.RecordRepository, Sample.SubmarineRepository, Sample.Characters, () => null,
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
        await statistics.LoadAsync();
        Render(new Views.StatisticsPageView { DataContext = statistics }, 1200, 1400,
            Path.Combine(directory, $"{stem}-statistics.png"));

        // 设置页的代理表单：看协议、地址、端口是不是分开摆的。
        SettingsPageViewModel settings = SettingsForm.Build();
        settings.SelectedProxy = settings.ProxyOptions.First(x => x.Mode == Domain.Settings.ProxyMode.Custom);
        Render(new Views.SettingsPageView { DataContext = settings }, 1100, 950,
            Path.Combine(directory, $"{stem}-settings.png"));

        // 收艇记账页的确认行：每一行带的艇归属下拉与艇名提示，看是不是塞得下、有没有被挤掉。
        ItemCatalog catalog = ItemCatalog.Load();
        CharacterManagerViewModel characters = new(Sample.Characters);
        await characters.LoadAsync();

        RecordsPageViewModel records = new(
            () => null,
            characters,
            Sample.RecordRepository,
            Sample.SubmarineRepository,
            new Lazy<ItemCatalog>(() => catalog),
            new ChatLogDump(Path.GetTempPath()));

        List<SubmarineChoice> choices = [new(string.Empty), new("Blackberry"), new("Blueberry")];
        records.LootRows.Add(LootRow(1, catalog, "沈没船の高級耳飾り", 5, "Blackberry", choices, string.Empty));
        records.LootRows.Add(LootRow(2, catalog, "ダイヤモンド", 27, "Blackberry", choices,
            "艇名读成「Blackberrvy」，已按「Blackberry」记。"));
        records.LootRows.Add(LootRow(3, catalog, "ルビー", 26, "Blackberry", choices,
            "艇名读成「Blug5erry」，可能是「Blueberry」，请选一下。"));

        Render(new Views.RecordsPageView { DataContext = records }, 1200, 900,
            Path.Combine(directory, $"{stem}-records.png"));
    }

    private static LootRowViewModel LootRow(
        int line,
        ItemCatalog catalog,
        string itemName,
        int quantity,
        string submarine,
        IReadOnlyList<SubmarineChoice> choices,
        string note)
    {
        LootCatalogEntry entry = catalog.Entries.First(x => x.AllNames.Contains(itemName));

        RecognizedLoot loot = new(
            line,
            itemName,
            entry,
            [entry],
            itemName,
            entry.ItemId,
            quantity,
            IsHighQuality: false,
            entry.ShopPriceNq,
            submarine,
            $"Tiina Branfordは {itemName}×{quantity}を入手した。");

        return new LootRowViewModel(loot, choices, note);
    }

    private static void Render(Control view, int width, int height, string path)
    {
        var window = new Window { Content = view, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        using (var stream = File.Create(path))
            frame.Save(stream, new PngBitmapEncoderOptions());

        window.Close();
    }
}
