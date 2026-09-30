using System.Globalization;
using Clean.Core.Formatting;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Clean.App.Controls;

public enum ChartValueKind
{
    Percent,
    Bytes,
}

// A single live series: the newest point is on the right, older ones slide left at every refresh.
public sealed partial class LiveLineChart : UserControl
{
    private const double AxisWidth = 56;
    private const double AxisHeight = 22;
    private const double TopPadding = 10;
    private const int GridLines = 4;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(object), typeof(LiveLineChart), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(LiveLineChart), new PropertyMetadata(0d, OnChanged));

    public static readonly DependencyProperty CapacityProperty =
        DependencyProperty.Register(nameof(Capacity), typeof(int), typeof(LiveLineChart), new PropertyMetadata(60, OnChanged));

    public static readonly DependencyProperty IntervalSecondsProperty =
        DependencyProperty.Register(nameof(IntervalSeconds), typeof(double), typeof(LiveLineChart), new PropertyMetadata(1.5, OnChanged));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(ChartValueKind), typeof(LiveLineChart), new PropertyMetadata(ChartValueKind.Percent, OnChanged));

    private readonly Canvas _canvas = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Line _crosshair = new() { StrokeThickness = 1, Stroke = new SolidColorBrush(ChartPalette.Axis), IsHitTestVisible = false };
    private readonly Ellipse _hoverDot = Dot();
    private readonly Border _tooltip;
    private readonly TextBlock _tooltipValue = new() { FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = new SolidColorBrush(ChartPalette.Ink) };
    private readonly TextBlock _tooltipTime = new() { FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(255, 0xC3, 0xC2, 0xB7)) };
    private List<Point> _points = [];
    private IReadOnlyList<double> _plotted = [];
    private double _maximum;

    public LiveLineChart()
    {
        _tooltip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x24, 0x24, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            IsHitTestVisible = false,
            Child = new StackPanel { Spacing = 1, Children = { _tooltipValue, _tooltipTime } },
        };

        Content = _canvas;
        SizeChanged += (_, _) => Redraw();
        _canvas.PointerMoved += (_, e) => ShowHover(e.GetCurrentPoint(_canvas).Position.X);
        _canvas.PointerExited += (_, _) => HideHover();
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    // Zero lets the chart pick a round maximum above the data.
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Capacity
    {
        get => (int)GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public double IntervalSeconds
    {
        get => (double)GetValue(IntervalSecondsProperty);
        set => SetValue(IntervalSecondsProperty, value);
    }

    public ChartValueKind Kind
    {
        get => (ChartValueKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((LiveLineChart)sender).Redraw();

    private void Redraw()
    {
        _canvas.Children.Clear();
        var width = ActualWidth - AxisWidth;
        var height = ActualHeight - AxisHeight - TopPadding;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _plotted = Values ?? [];
        _maximum = Maximum > 0 ? Maximum : NiceMaximum(_plotted.DefaultIfEmpty(0).Max());
        DrawGrid(width, height);
        DrawTimeAxis(width, height);

        var step = width / Math.Max(1, Capacity - 1);
        var offset = Capacity - _plotted.Count;
        _points = _plotted
            .Select((value, index) => new Point(AxisWidth + (offset + index) * step, TopPadding + height - Math.Clamp(value / _maximum, 0, 1) * height))
            .ToList();

        if (_points.Count >= 2)
        {
            DrawArea(height);
            var line = new Polyline { Stroke = new SolidColorBrush(ChartPalette.Series), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
            _points.ForEach(line.Points.Add);
            _canvas.Children.Add(line);
        }

        if (_points.Count > 0)
        {
            DrawLatest(_points[^1], _plotted[^1]);
        }

        _canvas.Children.Add(_crosshair);
        _canvas.Children.Add(_hoverDot);
        _canvas.Children.Add(_tooltip);
        HideHover();
    }

    private void DrawGrid(double width, double height)
    {
        for (var line = 0; line <= GridLines; line++)
        {
            var y = TopPadding + height - height * line / GridLines;
            _canvas.Children.Add(new Line
            {
                X1 = AxisWidth,
                X2 = AxisWidth + width,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(ChartPalette.Grid),
                StrokeThickness = 1,
            });

            var label = new TextBlock { Text = Format(_maximum * line / GridLines), FontSize = 11, Foreground = new SolidColorBrush(ChartPalette.Axis) };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, AxisWidth - 10 - label.DesiredSize.Width);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            _canvas.Children.Add(label);
        }
    }

    private void DrawTimeAxis(double width, double height)
    {
        var span = (Capacity - 1) * IntervalSeconds;
        (double Ratio, string Text)[] marks = [(0, $"-{span:0} s"), (0.5, $"-{span / 2:0} s"), (1, "maintenant")];
        foreach (var (ratio, text) in marks)
        {
            var label = new TextBlock { Text = text, FontSize = 11, Foreground = new SolidColorBrush(ChartPalette.Axis) };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var x = AxisWidth + width * ratio - label.DesiredSize.Width * ratio;
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, TopPadding + height + 6);
            _canvas.Children.Add(label);
        }
    }

    private void DrawArea(double height)
    {
        var baseline = TopPadding + height;
        var area = new Polygon
        {
            Fill = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = WithAlpha(ChartPalette.Series, 0x59), Offset = 0 },
                    new GradientStop { Color = WithAlpha(ChartPalette.Series, 0x00), Offset = 1 },
                },
            },
        };
        area.Points.Add(new Point(_points[0].X, baseline));
        _points.ForEach(area.Points.Add);
        area.Points.Add(new Point(_points[^1].X, baseline));
        _canvas.Children.Add(area);
    }

    // The newest value is labelled directly: it is the one people look for.
    private void DrawLatest(Point point, double value)
    {
        var dot = Dot();
        dot.Opacity = 1;
        Canvas.SetLeft(dot, point.X - dot.Width / 2);
        Canvas.SetTop(dot, point.Y - dot.Height / 2);
        _canvas.Children.Add(dot);

        var label = new TextBlock { Text = Format(value), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(ChartPalette.Ink) };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, point.X - label.DesiredSize.Width - 10);
        Canvas.SetTop(label, Math.Max(0, point.Y - label.DesiredSize.Height - 8));
        _canvas.Children.Add(label);
    }

    private void ShowHover(double x)
    {
        if (_points.Count == 0)
        {
            return;
        }

        var index = 0;
        for (var candidate = 1; candidate < _points.Count; candidate++)
        {
            if (Math.Abs(_points[candidate].X - x) < Math.Abs(_points[index].X - x))
            {
                index = candidate;
            }
        }

        var point = _points[index];
        _crosshair.X1 = _crosshair.X2 = point.X;
        _crosshair.Y1 = TopPadding;
        _crosshair.Y2 = ActualHeight - AxisHeight;
        _crosshair.Visibility = Visibility.Visible;

        Canvas.SetLeft(_hoverDot, point.X - _hoverDot.Width / 2);
        Canvas.SetTop(_hoverDot, point.Y - _hoverDot.Height / 2);
        _hoverDot.Opacity = 1;

        var secondsAgo = (_points.Count - 1 - index) * IntervalSeconds;
        _tooltipValue.Text = Format(_plotted[index]);
        _tooltipTime.Text = secondsAgo < 0.5 ? "maintenant" : $"il y a {secondsAgo.ToString("0", French)} s";
        _tooltip.Visibility = Visibility.Visible;
        _tooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var left = point.X + 12 + _tooltip.DesiredSize.Width > ActualWidth ? point.X - 12 - _tooltip.DesiredSize.Width : point.X + 12;
        Canvas.SetLeft(_tooltip, left);
        Canvas.SetTop(_tooltip, TopPadding);
    }

    private void HideHover()
    {
        _crosshair.Visibility = Visibility.Collapsed;
        _hoverDot.Opacity = 0;
        _tooltip.Visibility = Visibility.Collapsed;
    }

    private string Format(double value) => Kind == ChartValueKind.Bytes
        ? ByteSize.Format((long)Math.Max(0, value))
        : $"{value.ToString(value < 10 && value > 0 ? "0.#" : "0", French)} %";

    private double NiceMaximum(double value)
    {
        if (Kind == ChartValueKind.Percent)
        {
            // With four grid steps these give round labels: 5 %, 10 % or 25 % apart.
            return value <= 18 ? 20 : value <= 36 ? 40 : 100;
        }

        // Round up to 1, 2 or 5 times a power of two-ish byte unit, so grid labels stay readable.
        var target = Math.Max(value * 1.15, 1024 * 1024);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(target)));
        foreach (var factor in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (factor * magnitude >= target)
            {
                return factor * magnitude;
            }
        }

        return 10 * magnitude;
    }

    // 8 px marker with a 2 px surface ring, so it stays visible where it overlaps the line.
    private static Ellipse Dot() => new()
    {
        Width = 10,
        Height = 10,
        Fill = new SolidColorBrush(ChartPalette.Series),
        Stroke = new SolidColorBrush(ChartPalette.Surface),
        StrokeThickness = 2,
        IsHitTestVisible = false,
        Opacity = 0,
    };

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
