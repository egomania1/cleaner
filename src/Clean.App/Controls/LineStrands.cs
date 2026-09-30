using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Clean.App.Controls;

// Background decoration: two ribbons of thin lines drifting slowly behind the content.
// Each ribbon is drawn once over twice the control width with a wave that repeats every width,
// then slid sideways by a render transform animation, which runs on the compositor and costs no CPU per frame.
public sealed partial class LineStrands : UserControl
{
    private const int LinesPerRibbon = 12;
    private const int PointsPerWidth = 96;

    private static readonly UISettings Settings = new();

    // Cycles are whole numbers so the wave lines up with itself one width later and the loop has no visible seam.
    private static readonly Ribbon[] Ribbons =
    [
        new(BaseLine: 0.30, Amplitude: 0.10, Cycles: 2, SecondCycles: 3, Spread: 0.030, Duration: TimeSpan.FromSeconds(70), Reverse: false),
        new(BaseLine: 0.72, Amplitude: 0.08, Cycles: 3, SecondCycles: 1, Spread: 0.024, Duration: TimeSpan.FromSeconds(90), Reverse: true),
    ];

    private readonly Grid _root = new();
    private readonly List<(Canvas Layer, TranslateTransform Shift, Ribbon Ribbon)> _layers = [];
    private readonly Storyboard _drift = new() { RepeatBehavior = RepeatBehavior.Forever };
    private Size _drawnSize;

    public LineStrands()
    {
        IsHitTestVisible = false;
        IsTabStop = false;
        Content = _root;

        foreach (var ribbon in Ribbons)
        {
            var shift = new TranslateTransform();
            var layer = new Canvas { RenderTransform = shift };
            _root.Children.Add(layer);
            _layers.Add((layer, shift, ribbon));
        }

        SizeChanged += (_, args) => Redraw(args.NewSize);
        Unloaded += (_, _) => _drift.Stop();
        Loaded += (_, _) =>
        {
            if (_drawnSize.Width > 0)
            {
                _drift.Begin();
            }
        };
    }

    private void Redraw(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0 || size == _drawnSize)
        {
            return;
        }

        _drawnSize = size;
        _root.Clip = new RectangleGeometry { Rect = new Rect(default, size) };
        _drift.Stop();
        _drift.Children.Clear();

        foreach (var (layer, shift, ribbon) in _layers)
        {
            layer.Children.Clear();
            for (var index = 0; index < LinesPerRibbon; index++)
            {
                layer.Children.Add(CreateLine(ribbon, index, size));
            }

            var drift = new DoubleAnimation
            {
                From = ribbon.Reverse ? -size.Width : 0,
                To = ribbon.Reverse ? 0 : -size.Width,
                Duration = ribbon.Duration,
            };
            Storyboard.SetTarget(drift, shift);
            Storyboard.SetTargetProperty(drift, nameof(TranslateTransform.X));
            _drift.Children.Add(drift);
        }

        if (Settings.AnimationsEnabled && IsLoaded)
        {
            _drift.Begin();
        }
    }

    private static Polyline CreateLine(Ribbon ribbon, int index, Size size)
    {
        // Lines in the middle of a ribbon are brighter, so each ribbon fades out on its edges.
        var distanceFromCenter = Math.Abs(index - (LinesPerRibbon - 1) / 2.0) / (LinesPerRibbon / 2.0);
        var alpha = (byte)(10 + 26 * (1 - distanceFromCenter));
        var offset = index - (LinesPerRibbon - 1) / 2.0;
        var phase = index * 0.16;

        var points = new PointCollection();
        for (var step = 0; step <= PointsPerWidth * 2; step++)
        {
            var u = step / (double)PointsPerWidth;
            var angle = u * Math.PI * 2;

            // The spread breathes along the width, so strands fan out and gather like a loose cable.
            var spread = ribbon.Spread * (1 + 0.6 * Math.Sin(angle * ribbon.SecondCycles + 1.3));
            var y = ribbon.BaseLine
                + Math.Sin(angle * ribbon.Cycles + phase) * ribbon.Amplitude
                + Math.Sin(angle * ribbon.SecondCycles - phase * 0.5) * ribbon.Amplitude * 0.45
                + offset * spread;
            points.Add(new Point(u * size.Width, y * size.Height));
        }

        return new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
        };
    }

    private sealed record Ribbon(
        double BaseLine,
        double Amplitude,
        int Cycles,
        int SecondCycles,
        double Spread,
        TimeSpan Duration,
        bool Reverse);
}
