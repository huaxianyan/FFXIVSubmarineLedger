using System.Text.RegularExpressions;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 视图里的 <c>Grid.Column</c> / <c>Grid.Row</c> 不能超出所在 Grid 定义的列数/行数。
///
/// 写错列号时 Avalonia 不报错，控件会落到意外的位置——「已入账记录」那张表的金额就被写成
/// <c>Grid.Column="6"</c>，而那一行只有 5 列，于是金额压到了「删除」按钮上。这类问题靠看截图
/// 发现太晚，这里用静态检查钉住。
/// </summary>
public sealed partial class XamlGridColumnTests
{
    [Fact]
    public void EveryGridChildStaysInsideItsGrid()
    {
        string views = Path.Combine(AppContext.BaseDirectory, "Views");
        Assert.True(Directory.Exists(views), $"测试缺少视图副本：{views}");

        var problems = new List<string>();

        foreach (string file in Directory.EnumerateFiles(views, "*.axaml"))
        {
            string name = Path.GetFileName(file);
            problems.AddRange(Check(File.ReadAllText(file), name));
        }

        Assert.Empty(problems);
    }

    private static IEnumerable<string> Check(string xaml, string file)
    {
        // Grid 栈：每层记录它声明的列数、行数。自闭合的 Grid 不入栈。
        var stack = new List<(int Columns, int Rows)>();
        int line = 1;
        int scanned = 0;

        foreach (Match match in Tag().Matches(xaml))
        {
            line += CountNewlines(xaml, scanned, match.Index);
            scanned = match.Index;

            string tag = match.Value;
            bool closing = tag.StartsWith("</", StringComparison.Ordinal);

            if (closing)
            {
                // 只有 </Grid> 才出栈：别的闭合标签（StackPanel、Border…）也在这个 Grid 里面。
                if (tag.StartsWith("</Grid", StringComparison.Ordinal) && stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);

                continue;
            }

            if (tag.StartsWith("<Grid", StringComparison.Ordinal))
            {
                int columns = SpecCount(tag, "ColumnDefinitions");
                int rows = SpecCount(tag, "RowDefinitions");

                if (!tag.EndsWith("/>", StringComparison.Ordinal))
                    stack.Add((columns, rows));

                continue;
            }

            if (stack.Count == 0)
                continue;

            (int maxColumns, int maxRows) = stack[^1];

            Match column = GridColumn().Match(tag);
            if (column.Success && int.TryParse(column.Groups[1].Value, out int columnIndex) && columnIndex >= maxColumns)
                yield return $"{file}:{line} Grid.Column={columnIndex} 超出该 Grid 的 {maxColumns} 列";

            Match row = GridRow().Match(tag);
            if (row.Success && int.TryParse(row.Groups[1].Value, out int rowIndex) && rowIndex >= maxRows)
                yield return $"{file}:{line} Grid.Row={rowIndex} 超出该 Grid 的 {maxRows} 行";
        }

        // 收尾：别让一个 Grid 的开标签没有对应的闭标签（会让上面的检查失效）。
        _ = stack;
    }

    private static int CountNewlines(string text, int from, int to)
    {
        int count = 0;
        for (int i = from; i < to; i++)
        {
            if (text[i] == '\n')
                count++;
        }

        return count;
    }

    /// <summary>数 <c>ColumnDefinitions="A,B,C"</c> 里有几段。内联写法，不是简写 "*,*" 也算。</summary>
    private static int SpecCount(string tag, string attribute)
    {
        Match match = Regex.Match(tag, $"{attribute}=\"([^\"]*)\"");
        if (!match.Success)
            return 1;

        string spec = match.Groups[1].Value.Trim();
        if (spec.Length == 0)
            return 1;

        // "100,120,*,120,Auto" / "*,*" 都是按逗号分段
        return spec.Split(',').Length;
    }

    [GeneratedRegex(@"</?[A-Za-z][^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"Grid\.Column=""(\d+)""")]
    private static partial Regex GridColumn();

    [GeneratedRegex(@"Grid\.Row=""(\d+)""")]
    private static partial Regex GridRow();
}
