using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Records;
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

        LedgerPageViewModel ledger = BuildLedger();
        await ledger.LoadAsync();

        StatisticsPageViewModel statistics = new(ledger, () => null, new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
        statistics.Load();

        // 两个页面各出一张，同一份数据：明细看排版，统计看汇总。
        Render(new Views.LedgerPageView { DataContext = ledger }, 1200, 900,
            Path.Combine(Path.GetDirectoryName(output)!, "probe-ledger.png"));
        Render(new Views.StatisticsPageView { DataContext = statistics }, 1200, 1500, output);
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

    private static LedgerPageViewModel BuildLedger()
    {
        var character = new Character { Id = Guid.NewGuid(), Name = "Tiina Branford", World = "Titan", IsDefault = true };
        var submarine = new Submarine { Id = Guid.NewGuid(), CharacterId = character.Id, Name = "Blackberry" };

        var records = new List<LootRecord>();
        var today = new DateOnly(2026, 10, 29);
        for (int i = 0; i < 3; i++)
        {
            records.Add(new LootRecord
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                SubmarineId = submarine.Id,
                Date = today.AddDays(-i),
                Items =
                [
                    new LootItem { ItemId = 22507, ItemName = "沈没船の高級首飾り", Quantity = 5, UnitPrice = 34500 },
                    new LootItem { ItemId = 22506, ItemName = "沈没船の高級耳飾り", Quantity = 10, UnitPrice = 30000 },
                    new LootItem { ItemId = 24908, ItemName = "引き上げられた古銭", Quantity = 20, UnitPrice = 10 },
                ],
            });
        }

        return new LedgerPageViewModel(
            new FakeRecords(records),
            new FakeSubmarines([submarine]),
            new CharacterService(new FakeCharacters([character])),
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));
    }

    private sealed class FakeRecords(List<LootRecord> records) : ILootRecordRepository
    {
        public Task<IReadOnlyList<LootRecord>> ListAsync(
            DateOnly? from = null,
            DateOnly? to = null,
            Guid? characterId = null,
            CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LootRecord>>(records);

        public Task<LootRecord?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(records.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(LootRecord record, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSubmarines(List<Submarine> submarines) : ISubmarineRepository
    {
        public Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Submarine>>(submarines);

        public Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default) =>
            Task.FromResult(submarines.FirstOrDefault(x => x.CharacterId == characterId && x.Name == name));

        public Task AddAsync(Submarine submarine, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCharacters(List<Character> characters) : ICharacterRepository
    {
        public Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Character>>(characters);

        public Task<Character?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(characters.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(Character character, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Character character, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
