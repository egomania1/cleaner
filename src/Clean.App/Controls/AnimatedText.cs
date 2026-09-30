using System.Diagnostics;
using Clean.Core.Formatting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Clean.App.Controls;

public enum TextEffect
{
    CountUp,
    Decrypt,
}

// Plays a count up or a decrypt effect each time its text changes. Only use it for final values:
// text that changes many times per second, like live scan progress, would never settle.
public sealed partial class AnimatedText : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(AnimatedText), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty EffectProperty =
        DependencyProperty.Register(nameof(Effect), typeof(TextEffect), typeof(AnimatedText), new PropertyMetadata(TextEffect.Decrypt));

    public static readonly DependencyProperty IsShinyProperty =
        DependencyProperty.Register(nameof(IsShiny), typeof(bool), typeof(AnimatedText), new PropertyMetadata(false, OnIsShinyChanged));

    private static readonly UISettings Settings = new();
    private static readonly TimeSpan CountUpDuration = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan DecryptDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan ScrambleStep = TimeSpan.FromMilliseconds(40);

    private readonly TextBlock _block = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly Random _random = new();
    private readonly TextShine _shine;
    private long _start;
    private TimeSpan _lastScramble;
    private bool _isAnimating;

    public AnimatedText()
    {
        Content = _block;
        IsTabStop = false;
        _shine = new TextShine(this);
        Loaded += (_, _) =>
        {
            Play();
            if (IsShiny)
            {
                _shine.Start(TimeSpan.FromSeconds(1.2));
            }
        };
        Unloaded += (_, _) =>
        {
            StopAnimation();
            _shine.Stop();
        };
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public TextEffect Effect
    {
        get => (TextEffect)GetValue(EffectProperty);
        set => SetValue(EffectProperty, value);
    }

    public bool IsShiny
    {
        get => (bool)GetValue(IsShinyProperty);
        set => SetValue(IsShinyProperty, value);
    }

    public TextAlignment TextAlignment
    {
        get => _block.TextAlignment;
        set => _block.TextAlignment = value;
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((AnimatedText)sender).Play();

    private static void OnIsShinyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (AnimatedText)sender;
        control._shine.Clear();
        if ((bool)args.NewValue)
        {
            control._shine.Attach(control._block);
        }
        else
        {
            control._block.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private void Play()
    {
        var text = Text ?? string.Empty;
        if (!IsLoaded || !Settings.AnimationsEnabled || !TextEffects.CanAnimate(text))
        {
            StopAnimation();
            _block.Text = text;
            return;
        }

        _start = Stopwatch.GetTimestamp();
        _lastScramble = TimeSpan.Zero;
        ShowFrame(0);
        if (!_isAnimating)
        {
            CompositionTarget.Rendering += OnRendering;
            _isAnimating = true;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var elapsed = Stopwatch.GetElapsedTime(_start);
        var duration = Effect == TextEffect.CountUp ? CountUpDuration : DecryptDuration;
        var progress = Math.Min(elapsed / duration, 1);

        // Changing symbols on every frame is too fast to read as scrambling.
        if (Effect == TextEffect.Decrypt && progress < 1 && elapsed - _lastScramble < ScrambleStep)
        {
            return;
        }

        _lastScramble = elapsed;
        ShowFrame(progress);
        if (progress >= 1)
        {
            StopAnimation();
        }
    }

    private void ShowFrame(double progress)
    {
        var text = Text ?? string.Empty;
        _block.Text = Effect == TextEffect.CountUp
            ? TextEffects.CountUp(text, 1 - Math.Pow(1 - progress, 3))
            : TextEffects.Decrypt(text, progress, _random);
    }

    private void StopAnimation()
    {
        if (_isAnimating)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isAnimating = false;
        }
    }
}
