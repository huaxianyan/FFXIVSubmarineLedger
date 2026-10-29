using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FFXIVSubmarineLedger.Application.Records;

namespace FFXIVSubmarineLedger.Desktop.Controls;

/// <summary>
/// 收益走势折线。自绘而不引图表库：只需要一条折线加几个刻度，
/// 图表库的坐标轴、主题、包体积成本都不划算。
///
/// 数据里的空档由 <see cref="LootTrend"/> 补 0，所以折线不会把「那天没收益」画成斜线。
/// </summary>
public sealed class TrendChart : Control
{
    private const double PaddingLeft = 10;
    private const double PaddingRight = 10;
    private const double PaddingTop = 14;
    private const double PaddingBottom = 20;

    private static readonly IBrush LineBrush = new SolidColorBrush(Color.Parse("#5B8DEF"));
    private static readonly IBrush FillBrush = new SolidColorBrush(Color.Parse("#265B8DEF"));
    private static readonly IBrush GridBrush = new SolidColorBrush(Color.Parse("#22808080"));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#8C808080"));

    public static readonly StyledProperty<TrendSeries?> SeriesProperty =
        AvaloniaProperty.Register<TrendChart, TrendSeries?>(nameof(Series));

    static TrendChart()
    {
        AffectsRender<TrendChart>(SeriesProperty);
    }

    public TrendSeries? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        TrendSeries? series = Series;
        if (series is null || series.Points.Count == 0)
            return;

        double width = Bounds.Width;
        double height = Bounds.Height;
        double plotWidth = width - PaddingLeft - PaddingRight;
        double plotHeight = height - PaddingTop - PaddingBottom;

        if (plotWidth <= 4 || plotHeight <= 4)
            return;

        // 三条水平参考线，最高那条对应最大值。
        for (int i = 0; i < 3; i++)
        {
            double y = PaddingTop + (plotHeight * i / 2.0);
            context.DrawLine(new Pen(GridBrush, 1), new Point(PaddingLeft, y), new Point(width - PaddingRight, y));
        }

        long max = Math.Max(1, series.MaxValue);
        int count = series.Points.Count;

        Point Position(int index)
        {
            double x = count == 1
                ? PaddingLeft + (plotWidth / 2)
                : PaddingLeft + (plotWidth * index / (count - 1.0));

            double value = series.Points[index].TotalValue;
            double y = PaddingTop + plotHeight - (plotHeight * value / max);
            return new Point(x, y);
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(Position(0).X, PaddingTop + plotHeight), isFilled: true);
            for (int i = 0; i < count; i++)
                ctx.LineTo(Position(i));

            ctx.LineTo(new Point(Position(count - 1).X, PaddingTop + plotHeight));
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(FillBrush, null, geometry);

        var line = new StreamGeometry();
        using (StreamGeometryContext ctx = line.Open())
        {
            ctx.BeginFigure(Position(0), isFilled: false);
            for (int i = 1; i < count; i++)
                ctx.LineTo(Position(i));
            ctx.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, new Pen(LineBrush, 2), line);

        // 只有一个点时折线画不出来，补一个点。
        if (count == 1)
            context.DrawEllipse(LineBrush, null, Position(0), 3, 3);

        DrawCaption(context, FormatValue(max), new Point(PaddingLeft, 0));
        DrawCaption(context, FormatPeriod(series.Points[0].Period, series.Granularity), new Point(PaddingLeft, height - PaddingBottom + 4));
        DrawCaption(
            context,
            FormatPeriod(series.Points[count - 1].Period, series.Granularity),
            new Point(width - PaddingRight, height - PaddingBottom + 4),
            alignRight: true);
    }

    private static void DrawCaption(DrawingContext context, string text, Point origin, bool alignRight = false)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            11,
            TextBrush);

        Point position = alignRight
            ? new Point(origin.X - formatted.Width, origin.Y)
            : origin;

        context.DrawText(formatted, position);
    }

    private static string FormatValue(long value) => string.Create(CultureInfo.InvariantCulture, $"{value:N0}");

    private static string FormatPeriod(DateOnly period, TrendGranularity granularity) =>
        period.ToString(granularity == TrendGranularity.Day ? "MM-dd" : "yyyy-MM", CultureInfo.InvariantCulture);
}
