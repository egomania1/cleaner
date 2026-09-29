using System.Diagnostics;
using Clean.App.ViewModels;
using Clean.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.Controls;

public sealed partial class StorageDetailCard : UserControl
{
    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(object), typeof(StorageDetailCard), new PropertyMetadata(null, OnDetailChanged));

    private readonly long _startTimestamp = Stopwatch.GetTimestamp();
    private CardGradient? _gradient;
    private bool _isAnimating;

    public StorageDetailCard()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => StopAnimation();
    }

    public StorageDetailViewModel? Detail
    {
        get => (StorageDetailViewModel?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    private static void OnDetailChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var card = (StorageDetailCard)sender;
        card._gradient = e.NewValue is StorageDetailViewModel detail ? new CardGradient(detail.Kind, detail.Name) : null;
        card.GradientBase.Background = card._gradient?.Base;
        card.GradientGlow.Background = card._gradient?.Glow;
        card.UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (_gradient is not null && IsLoaded)
        {
            StartAnimation();
        }
        else
        {
            StopAnimation();
        }
    }

    private void StartAnimation()
    {
        if (!_isAnimating)
        {
            CompositionTarget.Rendering += OnRendering;
            _isAnimating = true;
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

    private void OnChildClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StorageUsage child })
        {
            Detail?.OpenChild(child);
        }
    }

    private void OnRendering(object? sender, object e) =>
        _gradient?.Animate(Stopwatch.GetElapsedTime(_startTimestamp));
}
