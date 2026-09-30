using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using Windows.UI.ViewManagement;

namespace Clean.App.Controls;

// Large condensed uppercase title with a small index label above it.
// Words rise into place one after another when the page opens, then a light band sweeps across them from time to time.
public sealed partial class PageTitle : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(PageTitle), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty IndexProperty =
        DependencyProperty.Register(nameof(Index), typeof(string), typeof(PageTitle), new PropertyMetadata(string.Empty, OnContentChanged));

    private const double TitleSize = 60;
    private static readonly UISettings Settings = new();
    private static readonly TimeSpan WordDuration = TimeSpan.FromMilliseconds(650);
    private static readonly TimeSpan WordStagger = TimeSpan.FromMilliseconds(90);

    private readonly TextBlock _label = new()
    {
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        CharacterSpacing = 240,
    };

    private readonly StackPanel _words = new() { Orientation = Orientation.Horizontal, Spacing = TitleSize * 0.22 };
    private readonly List<(TextBlock Word, TranslateTransform Shift)> _parts = [];
    private readonly TextShine _shine;
    private long _revealStart;
    private bool _isRevealing;

    public PageTitle()
    {
        IsTabStop = false;
        _label.Foreground = (Brush)Application.Current.Resources["MutedTextBrush"];
        Content = new StackPanel { Spacing = 10, Children = { _label, _words } };
        _shine = new TextShine(_words);
        Loaded += (_, _) => Reveal();
        Unloaded += (_, _) =>
        {
            StopReveal();
            _shine.Stop();
        };
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Index
    {
        get => (string)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    private static void OnContentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var title = (PageTitle)sender;
        title.Build();
        if (title.IsLoaded)
        {
            title.Reveal();
        }
    }

    private void Build()
    {
        _label.Text = string.IsNullOrEmpty(Index) ? "CLEAN" : $"{Index}  /  CLEAN";
        _shine.Clear();
        _words.Children.Clear();
        _parts.Clear();

        foreach (var word in (Text ?? string.Empty).ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var shift = new TranslateTransform();
            var block = new TextBlock
            {
                Text = word,
                FontFamily = new FontFamily("Bahnschrift"),
                FontStretch = FontStretch.Condensed,
                FontWeight = FontWeights.Bold,
                FontSize = TitleSize,
                LineHeight = TitleSize * 1.02,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                RenderTransform = shift,
            };
            _shine.Attach(block);
            _words.Children.Add(block);
            _parts.Add((block, shift));
        }
    }

    private void Reveal()
    {
        _shine.Stop();
        if (!Settings.AnimationsEnabled)
        {
            ApplyReveal(TimeSpan.MaxValue);
            _shine.Start(TimeSpan.FromSeconds(1));
            return;
        }

        _revealStart = Stopwatch.GetTimestamp();
        ApplyReveal(TimeSpan.Zero);
        if (!_isRevealing)
        {
            CompositionTarget.Rendering += OnRendering;
            _isRevealing = true;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var elapsed = Stopwatch.GetElapsedTime(_revealStart);
        if (ApplyReveal(elapsed))
        {
            StopReveal();
            _shine.Start(TimeSpan.FromMilliseconds(300));
        }
    }

    // Returns true once every word has arrived.
    private bool ApplyReveal(TimeSpan elapsed)
    {
        var done = true;
        for (var index = 0; index < _parts.Count; index++)
        {
            var local = elapsed == TimeSpan.MaxValue ? 1 : (elapsed - WordStagger * index) / WordDuration;
            var progress = Math.Clamp(local, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);

            _parts[index].Word.Opacity = eased;
            _parts[index].Shift.Y = (1 - eased) * TitleSize * 0.45;
            done &= progress >= 1;
        }

        return done;
    }

    private void StopReveal()
    {
        if (_isRevealing)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isRevealing = false;
        }
    }
}
