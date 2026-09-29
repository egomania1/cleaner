using Clean.Core.Formatting;
using Clean.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Clean.App.Controls;

public sealed partial class PolarChart : UserControl
{
    private const int ArcSteps = 8;
    private const int MaxLabelLength = 22;
    private const double DimmedOpacity = 0.4;

    private static readonly Color LowColor = Color.FromArgb(255, 0x10, 0x42, 0x81);
    private static readonly Color HighColor = Color.FromArgb(255, 0x9E, 0xC5, 0xF4);
    private static readonly SolidColorBrush GridBrush = new(Color.FromArgb(255, 0x2C, 0x2C, 0x2A));
    private static readonly SolidColorBrush LabelBrush = new(Colors.White);
    private static readonly SolidColorBrush MutedBrush = new(Color.FromArgb(255, 0x89, 0x87, 0x81));
    private static readonly SolidColorBrush HitBrush = new(Colors.Transparent);

    public static readonly DependencyProperty BucketsProperty =
        DependencyProperty.Register(nameof(Buckets), typeof(object), typeof(PolarChart), new PropertyMetadata(null, OnBucketsChanged));

    public static readonly DependencyProperty SelectedBucketProperty =
        DependencyProperty.Register(nameof(SelectedBucket), typeof(object), typeof(PolarChart), new PropertyMetadata(null, OnSelectedBucketChanged));

    private readonly Canvas _canvas = new();
    private readonly List<Microsoft.UI.Xaml.Shapes.Path> _wedges = [];
    private int? _hoveredIndex;

    public PolarChart()
    {
        Content = _canvas;
        SizeChanged += (_, _) => Redraw();
    }

    public event EventHandler<StorageUsage>? BucketSelected;

    public IReadOnlyList<StorageUsage>? Buckets
    {
        get => (IReadOnlyList<StorageUsage>?)GetValue(BucketsProperty);
        set => SetValue(BucketsProperty, value);
    }

    public StorageUsage? SelectedBucket
    {
        get => (StorageUsage?)GetValue(SelectedBucketProperty);
        set => SetValue(SelectedBucketProperty, value);
    }

    private static void OnBucketsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((PolarChart)sender).Redraw();

    private static void OnSelectedBucketChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((PolarChart)sender).UpdateHighlight();

    private void Redraw()
    {
        _canvas.Children.Clear();
        _wedges.Clear();
        _hoveredIndex = null;

        var buckets = Buckets;
        var width = ActualWidth;
        var height = ActualHeight;
        if (buckets is null || buckets.Count == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var center = new Point(width / 2, height / 2 + 6);
        var maxRadius = Math.Max(60, Math.Min(width, height) / 2 - 90);
        var minRadius = maxRadius * 0.16;
        var largestSize = buckets.Max(bucket => bucket.SizeBytes);
        var stepDegrees = 360.0 / buckets.Count;
        var gapDegrees = Math.Min(3, stepDegrees * 0.12);

        DrawGridCircles(center, maxRadius);

        for (var index = 0; index < buckets.Count; index++)
        {
            var bucket = buckets[index];
            var startDegrees = index * stepDegrees;
            var ratio = largestSize > 0 ? (double)bucket.SizeBytes / largestSize : 0;
            var radius = minRadius + (maxRadius - minRadius) * Math.Sqrt(ratio);

            var wedge = CreateSector(center, radius, startDegrees + gapDegrees / 2, startDegrees + stepDegrees - gapDegrees / 2);
            wedge.Fill = new SolidColorBrush(Blend(ratio));
            _canvas.Children.Add(wedge);
            _wedges.Add(wedge);

            DrawSpoke(center, maxRadius, startDegrees);
            var label = DrawLabel(center, maxRadius + 24, startDegrees + stepDegrees / 2, bucket);

            // The whole slice, up to the outer circle, reacts to the mouse so that small entries stay easy to click.
            var hitArea = CreateSector(center, maxRadius, startDegrees, startDegrees + stepDegrees);
            hitArea.Fill = HitBrush;
            _canvas.Children.Add(hitArea);
            MakeInteractive(hitArea, index, bucket);
            MakeInteractive(label, index, bucket);
        }

        DrawLegend(height);
        UpdateHighlight();
    }

    private void MakeInteractive(UIElement element, int index, StorageUsage bucket)
    {
        element.PointerEntered += (_, _) => SetHovered(index);
        element.PointerExited += (_, _) => SetHovered(null);
        element.Tapped += (_, _) => BucketSelected?.Invoke(this, bucket);
    }

    private void SetHovered(int? index)
    {
        _hoveredIndex = index;
        ProtectedCursor = InputSystemCursor.Create(index is null ? InputSystemCursorShape.Arrow : InputSystemCursorShape.Hand);
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        var buckets = Buckets;
        var selectedIndex = buckets is null || SelectedBucket is null ? -1 : IndexOf(buckets, SelectedBucket);
        var hasFocus = selectedIndex >= 0 || _hoveredIndex is not null;

        for (var index = 0; index < _wedges.Count; index++)
        {
            var isSelected = index == selectedIndex;
            var isHovered = index == _hoveredIndex;

            _wedges[index].Opacity = !hasFocus || isSelected || isHovered ? 1 : DimmedOpacity;
            _wedges[index].Stroke = isSelected ? LabelBrush : null;
            _wedges[index].StrokeThickness = isSelected ? 2 : 0;
        }
    }

    private static int IndexOf(IReadOnlyList<StorageUsage> buckets, StorageUsage bucket)
    {
        for (var index = 0; index < buckets.Count; index++)
        {
            if (buckets[index] == bucket)
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawGridCircles(Point center, double maxRadius)
    {
        foreach (var fraction in new[] { 0.33, 0.66, 1.0 })
        {
            var radius = maxRadius * fraction;
            var circle = new Ellipse { Width = radius * 2, Height = radius * 2, Stroke = GridBrush, StrokeThickness = 1 };
            Place(circle, center.X - radius, center.Y - radius);
        }
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreateSector(Point center, double radius, double startDegrees, double endDegrees)
    {
        var figure = new PathFigure { StartPoint = center, IsClosed = true, IsFilled = true };
        for (var step = 0; step <= ArcSteps; step++)
        {
            var bearing = startDegrees + (endDegrees - startDegrees) * step / ArcSteps;
            figure.Segments.Add(new LineSegment { Point = ToPoint(center, radius, bearing) });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Microsoft.UI.Xaml.Shapes.Path { Data = geometry };
    }

    private void DrawSpoke(Point center, double maxRadius, double bearing)
    {
        var end = ToPoint(center, maxRadius, bearing);
        _canvas.Children.Add(new Line { X1 = center.X, Y1 = center.Y, X2 = end.X, Y2 = end.Y, Stroke = GridBrush, StrokeThickness = 1 });
    }

    private StackPanel DrawLabel(Point center, double radius, double bearing, StorageUsage bucket)
    {
        var alignment = bearing < 8 || bearing > 352 || Math.Abs(bearing - 180) < 8
            ? HorizontalAlignment.Center
            : bearing < 180 ? HorizontalAlignment.Left : HorizontalAlignment.Right;

        var name = bucket.Label.Length > MaxLabelLength ? bucket.Label[..(MaxLabelLength - 1)] + "…" : bucket.Label;
        var label = new StackPanel
        {
            Background = HitBrush,
            Children = { CreateText(name, alignment), CreateText(ByteSize.Format(bucket.SizeBytes), alignment) },
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var anchor = ToPoint(center, radius, bearing);
        var left = alignment switch
        {
            HorizontalAlignment.Left => anchor.X,
            HorizontalAlignment.Right => anchor.X - label.DesiredSize.Width,
            _ => anchor.X - label.DesiredSize.Width / 2,
        };
        Place(label, left, anchor.Y - 12);
        return label;
    }

    private void DrawLegend(double height)
    {
        const double Left = 20;
        const double Width = 140;
        var top = height - 34;

        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        gradient.GradientStops.Add(new GradientStop { Color = LowColor, Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = HighColor, Offset = 1 });
        Place(new Rectangle { Width = Width, Height = 10, Fill = gradient }, Left, top);

        Place(new TextBlock { Text = "Petit", FontSize = 10, Foreground = MutedBrush }, Left, top - 18);
        var large = new TextBlock { Text = "Grand", FontSize = 10, Foreground = MutedBrush };
        large.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Place(large, Left + Width - large.DesiredSize.Width, top - 18);
    }

    private void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        _canvas.Children.Add(element);
    }

    private static TextBlock CreateText(string text, HorizontalAlignment alignment) =>
        new() { Text = text, FontSize = 11, Foreground = LabelBrush, HorizontalAlignment = alignment };

    // Bearings are measured clockwise from 12 o'clock, like on a compass.
    private static Point ToPoint(Point center, double radius, double bearingDegrees)
    {
        var radians = bearingDegrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Sin(radians), center.Y - radius * Math.Cos(radians));
    }

    private static Color Blend(double ratio)
    {
        static byte Mix(byte from, byte to, double ratio) => (byte)Math.Round(from + (to - from) * ratio);

        ratio = Math.Clamp(ratio, 0, 1);
        return Color.FromArgb(255, Mix(LowColor.R, HighColor.R, ratio), Mix(LowColor.G, HighColor.G, ratio), Mix(LowColor.B, HighColor.B, ratio));
    }
}
