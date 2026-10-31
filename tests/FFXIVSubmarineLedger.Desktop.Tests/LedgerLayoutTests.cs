using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FFXIVSubmarineLedger.Application.Abstractions;
using FFXIVSubmarineLedger.Application.Characters;
using FFXIVSubmarineLedger.Domain.Characters;
using FFXIVSubmarineLedger.Domain.Records;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using FFXIVSubmarineLedger.Infrastructure.Catalog;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 收艇记录页的排版：金额不能压到「删除」按钮上，角色名（含服务器）不能被省略号截掉。
///
/// 这两件事靠肉眼看截图才发现太晚，这里直接量真实排版后的边界。
/// </summary>
public sealed class LedgerLayoutTests
{
    [AvaloniaFact]
    public void AmountDoesNotOverlapTheDeleteButton()
    {
        (Window window, _) = Show();

        var deleteButton = window.GetVisualDescendants().OfType<Button>()
            .First(x => x.Content as string is "删除");
        TextBlock amount = Label(window, x => x.Text?.Contains("万 Gil", StringComparison.Ordinal) == true
                                               || x.Text?.EndsWith(" Gil", StringComparison.Ordinal) == true);

        Rect button = Bounds(window, deleteButton);
        Rect money = Bounds(window, amount);

        Assert.True(amount.Bounds.Width > 0, "金额没被渲染出来");
        Assert.True(money.Right <= button.Left,
            $"金额右边缘 {money.Right:0.#} 越过了删除按钮左边缘 {button.Left:0.#}");
        Assert.True(money.Intersects(button) == false, "金额和删除按钮重叠了");

        window.Close();
    }

    [AvaloniaFact]
    public void CharacterLabelShowsBothNameAndWorld()
    {
        (Window window, _) = Show();

        // 两行块：第一行艇名，第二行「角色 @ 服务器」。第二行不能被截断。
        TextBlock character = Label(window, x => x.Text?.Contains(" @ ", StringComparison.Ordinal) == true);

        string full = character.Text!;
        double needed = Measure(character, full);

        Assert.True(character.Bounds.Width + 1 >= needed,
            $"角色行宽 {character.Bounds.Width:0.#} 放不下「{full}」需要的 {needed:0.#}，会被省略号截掉");

        window.Close();
    }

    [AvaloniaFact]
    public void SubmarineNameIsAboveTheCharacterLine()
    {
        (Window window, _) = Show();

        TextBlock submarine = Label(window, x => x.Text == "Blackberry");
        TextBlock character = Label(window, x => x.Text?.Contains(" @ ", StringComparison.Ordinal) == true);

        Assert.True(submarine.Bounds.Top < character.Bounds.Top, "艇名应该在角色行上方");

        window.Close();
    }

    [AvaloniaFact]
    public void NewestRecordComesFirst()
    {
        (Window window, LedgerPageViewModel ledger) = Show();

        List<DateOnly> dates = [.. ledger.Records.Select(x => DateOnly.Parse(x.DateLabel))];
        Assert.Equal(dates.OrderByDescending(x => x), dates);

        window.Close();
    }

    private static double Measure(TextBlock reference, string text)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(reference.FontFamily, reference.FontStyle, reference.FontWeight),
            reference.FontSize,
            Brushes.Black);

        return formatted.Width;
    }

    private static TextBlock Label(Window window, Func<TextBlock, bool> predicate) =>
        window.GetVisualDescendants().OfType<TextBlock>().First(predicate);

    /// <summary>控件在自己那个窗口坐标系里的位置，跨层比较用。</summary>
    private static Rect Bounds(Window window, Control control)
    {
        Point origin = control.TranslatePoint(default, window) ?? default;
        return new Rect(origin, control.Bounds.Size);
    }

    private static (Window Window, LedgerPageViewModel Ledger) Show()
    {
        var character = new Character { Id = Guid.NewGuid(), Name = "Tiina Branford", World = "Titan" };
        var submarine = new Submarine { Id = Guid.NewGuid(), CharacterId = character.Id, Name = "Blackberry" };

        var record = new LootRecord
        {
            Id = Guid.NewGuid(),
            CharacterId = character.Id,
            SubmarineId = submarine.Id,
            Date = new DateOnly(2026, 10, 29),
            Items =
            [
                new LootItem { ItemId = 22507, ItemName = "沈没船の高級首飾り", Quantity = 3, UnitPrice = 34500 },
            ],
        };

        var ledger = new LedgerPageViewModel(
            new Records([record]),
            new Submarines([submarine]),
            new CharacterService(new Characters([character])),
            new Lazy<ItemCatalog>(() => ItemCatalog.Load()));

        ledger.LoadAsync().GetAwaiter().GetResult();

        var window = new Window
        {
            Content = new Views.LedgerPageView { DataContext = ledger },
            Width = 1200,
            Height = 700,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, ledger);
    }

    private sealed class Records(List<LootRecord> rows) : ILootRecordRepository
    {
        public Task<IReadOnlyList<LootRecord>> ListAsync(
            DateOnly? from = null, DateOnly? to = null, Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LootRecord>>(rows);

        public Task<LootRecord?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(rows.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(LootRecord record, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Submarines(List<Submarine> rows) : ISubmarineRepository
    {
        public Task<IReadOnlyList<Submarine>> ListAsync(Guid? characterId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Submarine>>(rows);

        public Task<Submarine?> FindByNameAsync(Guid characterId, string name, CancellationToken ct = default) =>
            Task.FromResult(rows.FirstOrDefault(x => x.CharacterId == characterId && x.Name == name));

        public Task AddAsync(Submarine submarine, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Characters(List<Character> rows) : ICharacterRepository
    {
        public Task<IReadOnlyList<Character>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Character>>(rows);

        public Task<Character?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(rows.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(Character character, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Character character, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
