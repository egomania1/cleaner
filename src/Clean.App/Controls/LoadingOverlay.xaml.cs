using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace Clean.App.Controls;

// Startup is too short and too uneven to measure, so the counter is paced by time:
// it slows down near 90 % while the app is still loading, then runs to 100 % once CompleteAsync is called.
public sealed partial class LoadingOverlay : UserControl
{
    private const double WaitingCeiling = 90;
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan WaitingPace = TimeSpan.FromSeconds(0.9);
    private static readonly TimeSpan FinishingPace = TimeSpan.FromSeconds(0.12);

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(LoadingOverlay), new PropertyMetadata(string.Empty));

    private readonly long _start = Stopwatch.GetTimestamp();
    private TaskCompletionSource? _finished;
    private TimeSpan _lastFrame;
    private double _progress;
    private int _shownPercent = -1;

    public LoadingOverlay()
    {
        InitializeComponent();
        Loaded += (_, _) => CompositionTarget.Rendering += OnRendering;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public Task CompleteAsync()
    {
        _finished ??= new TaskCompletionSource();
        return _finished.Task;
    }

    public Task FadeOutAsync()
    {
        var completion = new TaskCompletionSource();
        var fade = new DoubleAnimation { To = 0, Duration = FadeDuration };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade, nameof(Opacity));

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Completed += (_, _) => completion.SetResult();
        storyboard.Begin();

        return completion.Task;
    }

    private void OnRendering(object? sender, object e)
    {
        var elapsed = Stopwatch.GetElapsedTime(_start);
        var frame = elapsed - _lastFrame;
        _lastFrame = elapsed;

        var isFinishing = _finished is not null;
        var target = isFinishing ? 100 : WaitingCeiling * (1 - Math.Exp(-elapsed.TotalSeconds / 1.5));
        var pace = isFinishing ? FinishingPace : WaitingPace;
        _progress += (target - _progress) * (1 - Math.Exp(-frame / pace));

        if (isFinishing && _progress > 99.5)
        {
            _progress = 100;
        }

        Show(_progress);

        if (_progress >= 100 && _finished is { Task.IsCompleted: false })
        {
            _finished.SetResult();
        }
    }

    private void Show(double progress)
    {
        FillScale.ScaleX = progress / 100;

        var percent = (int)progress;
        if (percent != _shownPercent)
        {
            _shownPercent = percent;
            PercentText.Text = $"{percent} %";
        }
    }
}
