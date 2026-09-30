using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Clean.App.Controls;

// A tiny trend line for a table row: no axis, the row's text gives the value.
public sealed partial class Sparkline : UserControl
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(object), typeof(Sparkline), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(Sparkline), new PropertyMetadata(100d, OnChanged));

    public static readonly DependencyProperty CapacityProperty =
        DependencyProperty.Register(nameof(Capacity), typeof(int), typeof(Sparkline), new PropertyMetadata(40, OnChanged));

    private readonly Canvas _canvas = new();

    public Sparkline()
    {
        Content = _canvas;
        IsHitTestVisible = false;
        SizeChanged += (_, _) => Redraw();
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

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

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((Sparkline)sender).Redraw();

    private void Redraw()
    {
        _canvas.Children.Clear();
        var values = Values;
        if (values is null || values.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var maximum = Maximum > 0 ? Maximum : 1;
        var step = ActualWidth / Math.Max(1, Capacity - 1);
        var offset = Capacity - values.Count;
        var bottom = ActualHeight - 1;
        var points = values
            .Select((value, index) => new Point((offset + index) * step, bottom - Math.Clamp(value / maximum, 0, 1) * (ActualHeight - 2)))
            .ToList();

        var area = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(0x33, ChartPalette.Series.R, ChartPalette.Series.G, ChartPalette.Series.B)) };
        area.Points.Add(new Point(points[0].X, bottom));
        points.ForEach(area.Points.Add);
        area.Points.Add(new Point(points[^1].X, bottom));
        _canvas.Children.Add(area);

        var line = new Polyline { Stroke = new SolidColorBrush(ChartPalette.Series), StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round };
        points.ForEach(line.Points.Add);
        _canvas.Children.Add(line);
    }
}
