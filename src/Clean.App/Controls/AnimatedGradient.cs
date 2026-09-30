using System.Diagnostics;
using Clean.Core.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.Controls;

// The Stockage detail card's moving gradient, as a standalone panel: each seed gets its own stable motion.
public sealed partial class AnimatedGradient : UserControl
{
    public static readonly DependencyProperty SeedProperty =
        DependencyProperty.Register(nameof(Seed), typeof(string), typeof(AnimatedGradient), new PropertyMetadata(null, OnSeedChanged));

    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private readonly Border _base = new();
    private readonly Border _glow = new() { Opacity = 0.8 };
    private CardGradient? _gradient;
    private bool _isAnimating;

    public AnimatedGradient()
    {
        Content = new Grid { Children = { _base, _glow } };
        RegisterPropertyChangedCallback(CornerRadiusProperty, (_, _) => _base.CornerRadius = _glow.CornerRadius = CornerRadius);
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => StopAnimation();
    }

    public LocationKind Kind { get; set; } = LocationKind.Applications;

    public string? Seed
    {
        get => (string?)GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    private static void OnSeedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var panel = (AnimatedGradient)sender;
        panel._gradient = e.NewValue is string seed ? new CardGradient(panel.Kind, seed) : null;
        panel._base.Background = panel._gradient?.Base;
        panel._glow.Background = panel._gradient?.Glow;
        panel.UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        // The Windows "animation effects" setting is honoured: the gradient then stays still.
        if (_gradient is not null && IsLoaded && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            if (!_isAnimating)
            {
                CompositionTarget.Rendering += OnRendering;
                _isAnimating = true;
            }
        }
        else
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

    private void OnRendering(object? sender, object e) => _gradient?.Animate(Stopwatch.GetElapsedTime(_startTimestamp));
}
