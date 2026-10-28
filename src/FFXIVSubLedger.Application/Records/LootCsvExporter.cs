using System.Globalization;
using System.Text;
using FFXIVSubLedger.Application.Localization;
using FFXIVSubLedger.Domain.Records;

namespace FFXIVSubLedger.Application.Records;

/// <summary>
/// 把已入账记录导成 CSV：一行一条道具明细，交给 Excel 或数透表用。
///
/// 带 UTF-8 BOM：不带的话 Excel 会把中日文读成乱码，这类「导出后还得手动转码」的事不该让用户做。
/// </summary>
public static class LootCsvExporter
{
    /// <param name="records">要导出的记录。</param>
    /// <param name="characterNames">角色名。</param>
    /// <param name="resolveItemName">把（道具 ID、落库名）换成显示名。不给就原样输出落库名。</param>
    public static string ToCsv(
        IReadOnlyList<LootRecord> records,
        IReadOnlyDictionary<Guid, string> characterNames,
        Func<uint, string, string>? resolveItemName = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(characterNames);

        resolveItemName ??= static (_, stored) => stored;

        var builder = new StringBuilder();

        foreach (string column in Header())
            builder.Append(Escape(column)).Append(',');
        builder.Length -= 1;
        builder.Append("\r\n");

        foreach (LootRecord record in records)
        {
            string character = characterNames.GetValueOrDefault(
                record.CharacterId,
                Loc.Get("stats.unknownCharacter"));

            foreach (LootItem item in record.Items)
            {
                builder
                    .Append(record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(character)).Append(',')
                    .Append(Escape(record.FreeCompanyId is null ? string.Empty : record.FreeCompanyId.Value.ToString())).Append(',')
                    .Append(Escape(resolveItemName(item.ItemId, item.ItemName))).Append(',')
                    .Append(item.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.IsHighQuality ? "HQ" : "NQ").Append(',')
                    .Append(item.UnitPrice.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(item.Subtotal.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Escape(record.Note))
                    .Append("\r\n");
            }
        }

        return builder.ToString();
    }

    /// <summary>带 BOM 写文件，Excel 才认得出 UTF-8。</summary>
    public static async Task WriteAsync(string path, string csv, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct)
            .ConfigureAwait(false);
    }

    /// <summary>逗号、引号、换行都要按 CSV 规矩转义。</summary>
    private static string[] Header() =>
    [
        Loc.Get("csv.date"),
        Loc.Get("csv.character"),
        Loc.Get("csv.freeCompany"),
        Loc.Get("csv.item"),
        Loc.Get("csv.quantity"),
        Loc.Get("csv.quality"),
        Loc.Get("csv.unitPrice"),
        Loc.Get("csv.subtotal"),
        Loc.Get("csv.note"),
    ];

    private static string Escape(string value)
    {
        if (value.Length == 0)
            return value;

        bool needsQuotes = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal);

        return needsQuotes
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
