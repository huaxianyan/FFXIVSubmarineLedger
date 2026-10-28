using FFXIVSubmarineLedger.Application.Records;
using FFXIVSubmarineLedger.Domain.Records;

namespace FFXIVSubmarineLedger.Domain.Tests.Records;

public sealed class LootCsvExporterTests
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ToCsv_WritesOneRowPerItemWithHeader()
    {
        var records = new List<LootRecord>
        {
            new()
            {
                CharacterId = Alice,
                Date = new DateOnly(2026, 10, 27),
                Items =
                [
                    new LootItem { ItemId = 22507, ItemName = "沈没船の高級首飾り", Quantity = 5, UnitPrice = 34500 },
                    new LootItem { ItemId = 5189, ItemName = "エメラルド", Quantity = 25, IsHighQuality = true, UnitPrice = 3 },
                ],
            },
        };

        string csv = LootCsvExporter.ToCsv(records, new Dictionary<Guid, string> { [Alice] = "Alice" });
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("日期,角色,部队,道具,数量,品质,单价,小计,备注", lines[0], StringComparison.Ordinal);
        Assert.Equal("2026-10-27,Alice,,沈没船の高級首飾り,5,NQ,34500,172500,", lines[1]);
        Assert.Equal("2026-10-27,Alice,,エメラルド,25,HQ,3,75,", lines[2]);
    }

    [Fact]
    public void ToCsv_EscapesCommasAndQuotesInItemNames()
    {
        var records = new List<LootRecord>
        {
            new()
            {
                CharacterId = Alice,
                Date = new DateOnly(2026, 10, 27),
                Items = [new LootItem { ItemName = "名字带,逗号\"与引号\"的道具", Quantity = 1, UnitPrice = 2 }],
            },
        };

        string csv = LootCsvExporter.ToCsv(records, new Dictionary<Guid, string> { [Alice] = "Alice" });

        Assert.Contains("\"名字带,逗号\"\"与引号\"\"的道具\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ToCsv_NamesUnknownCharactersInsteadOfLeavingTheCellBlank()
    {
        var records = new List<LootRecord>
        {
            new()
            {
                CharacterId = Alice,
                Date = new DateOnly(2026, 10, 27),
                Items = [new LootItem { ItemName = "エメラルド", Quantity = 1, UnitPrice = 2 }],
            },
        };

        string csv = LootCsvExporter.ToCsv(records, new Dictionary<Guid, string>());

        Assert.Contains("未知角色", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_PrependsTheBomExcelNeeds()
    {
        string path = Path.Combine(Path.GetTempPath(), $"loot-{Guid.NewGuid():N}.csv");

        try
        {
            await LootCsvExporter.WriteAsync(path, "日期\r\n");

            byte[] head = (await File.ReadAllBytesAsync(path))[..3];
            Assert.Equal([0xEF, 0xBB, 0xBF], head);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
