using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Clean.App.Controls;

public sealed partial class SegmentedProgress : UserControl
{
    private const int SegmentCount = 20;
    private const int HoverReach = 3;
    private static readonly TimeSpan FillDuration = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(250);

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SegmentedProgress), new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SegmentedProgress), new PropertyMetadata(string.Empty));

    private readonly List<Border> _segments = [];
    private readonly Brush _filledBrush;
    private readonly Brush _emptyBrush;

    private double _displayedValue;
    private double _animationFrom;
    private double _animationTo;
    private long _animationStart;
    private bool _isAnimating;

    public SegmentedProgress()
    {
        InitializeComponent();
        _filledBrush = (Brush)Application.Current.Resources["SegmentFilledBrush"];
        _emptyBrush = (Brush)Application.Current.Resources["SegmentEmptyBrush"];
        CreateSegments();
        Unloaded += (_, _) => StopAnimation();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((SegmentedProgress)sender).AnimateTo(Math.Clamp((double)e.NewValue, 0, 100));

    private void CreateSegments()
    {
        for (var index = 0; index < SegmentCount; index++)
        {
            SegmentsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var segment = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = _emptyBrush,
                BackgroundTransition = new BrushTransition { Duration = TimeSpan.FromMilliseconds(300) },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(),
            };

            var segmentIndex = index;
            segment.PointerEntered += (_, _) => ApplyHover(segmentIndex);
            segment.PointerExited += (_, _) => ApplyHover(null);

            Grid.SetColumn(segment, index);
            SegmentsGrid.Children.Add(segment);
            _segments.Add(segment);
        }
    }

    private void AnimateTo(double target)
    {
        _animationFrom = _displayedValue;
        _animationTo = target;
        _animationStart = Stopwatch.GetTimestamp();

        if (!_isAnimating)
        {
            CompositionTarget.Rendering += OnRendering;
            _isAnimating = true;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var progress = Math.Min(Stopwatch.GetElapsedTime(_animationStart) / FillDuration, 1);
        var easedProgress = 1 - Math.Pow(1 - progress, 3);
        ShowValue(_animationFrom + (_animationTo - _animationFrom) * easedProgress);

        if (progress >= 1)
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        if (_isAnimating)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isAnimating = false;
        }
    }

    private void ShowValue(double value)
    {
        _displayedValue = value;
        PercentText.Text = $"{Math.Round(value)} %";

        var filledCount = (int)Math.Round(value / 100 * SegmentCount);
        for (var index = 0; index < _segments.Count; index++)
        {
            _segments[index].Background = index < filledCount ? _filledBrush : _emptyBrush;
        }
    }

    private void ApplyHover(int? hoveredIndex)
    {
        for (var index = 0; index < _segments.Count; index++)
        {
            AnimateScale(_segments[index], HoverScale(index, hoveredIndex));
        }
    }

    private static double HoverScale(int index, int? hoveredIndex)
    {
        if (hoveredIndex is null)
        {
            return 1;
        }

        var distance = Math.Abs(hoveredIndex.Value - index);
        if (distance == 0)
        {
            return 1.3;
        }

        if (distance > HoverReach)
        {
            return 1;
        }

        var falloff = Math.Cos((double)distance / HoverReach * (Math.PI / 2));
        return 1 + 0.2 * falloff;
    }

    private static void AnimateScale(Border segment, double scale)
    {
        var animation = new DoubleAnimation
        {
            To = scale,
            Duration = HoverDuration,
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 },
        };
        Storyboard.SetTarget(animation, segment.RenderTransform);
        Storyboard.SetTargetProperty(animation, nameof(ScaleTransform.ScaleY));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
