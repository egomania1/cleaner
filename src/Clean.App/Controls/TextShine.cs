using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Clean.App.Controls;

// A band of light that sweeps across one or more text blocks every few seconds.
// Each block gets its own gradient, shifted by the block's position, so the band looks continuous across words.
internal sealed class TextShine
{
    private static readonly TimeSpan SweepDuration = TimeSpan.FromSeconds(1.6);
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(5);
    private static readonly Color Base = Color.FromArgb(255, 0xFA, 0xFA, 0xFA);
    private static readonly Color Glow = Color.FromArgb(255, 0x9A, 0x9A, 0xFF);
    private static readonly Color Core = Color.FromArgb(255, 0x6B, 0x6B, 0xFF);

    private readonly FrameworkElement _host;
    private readonly List<(TextBlock Block, LinearGradientBrush Brush)> _parts = [];
    private readonly DispatcherTimer _timer = new() { Interval = Pause };
    private long _sweepStart;
    private bool _isSweeping;

    public TextShine(FrameworkElement host)
    {
        _host = host;
        _timer.Tick += (_, _) => StartSweep();
    }

    public void Attach(TextBlock block)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(-1000, 0),
            EndPoint = new Point(-999, 0),
            GradientStops =
            {
                new GradientStop { Color = Base, Offset = 0 },
                new GradientStop { Color = Glow, Offset = 0.35 },
                new GradientStop { Color = Core, Offset = 0.5 },
                new GradientStop { Color = Glow, Offset = 0.65 },
                new GradientStop { Color = Base, Offset = 1 },
            },
        };
        block.Foreground = brush;
        _parts.Add((block, brush));
    }

    public void Clear()
    {
        Stop();
        _parts.Clear();
    }

    public void Start(TimeSpan firstDelay)
    {
        _timer.Interval = firstDelay;
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_isSweeping)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isSweeping = false;
        }
    }

    private void StartSweep()
    {
        _timer.Interval = Pause + SweepDuration;
        _sweepStart = Stopwatch.GetTimestamp();
        if (!_isSweeping)
        {
            CompositionTarget.Rendering += OnRendering;
            _isSweeping = true;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var progress = Stopwatch.GetElapsedTime(_sweepStart) / SweepDuration;
        var width = _host.ActualWidth;
        var band = Math.Max(width * 0.18, 60);
        var center = -band + (width + 2 * band) * Math.Min(progress, 1);

        foreach (var (block, brush) in _parts)
        {
            var offset = block.TransformToVisual(_host).TransformPoint(default).X;
            brush.StartPoint = new Point(center - band - offset, 0);
            brush.EndPoint = new Point(center + band - offset, 0);
        }

        if (progress >= 1)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isSweeping = false;
        }
    }
}
