using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace Clean.App.Controls;

// XAML has no conic gradient, so the rotating border light is drawn into a small bitmap
// that is used as the border brush and spun with a rotate transform.
public sealed partial class ShinyButton : Button
{
    private const int TextureSize = 128;
    private const double RestSpread = 0.05;
    private const double HoverSpread = 0.20;
    private const double HoverAngleShift = 95;

    private static readonly TimeSpan SpinDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HoverDuration = TimeSpan.FromMilliseconds(800);
    private static readonly Color Highlight = Color.FromArgb(255, 0x00, 0x00, 0xFF);
    private static readonly Color HighlightSubtle = Color.FromArgb(255, 0x84, 0x84, 0xFF);
    private static readonly Color Shine = Color.FromArgb(255, 0xFF, 0xFF, 0xFF);

    private readonly byte[] _pixels = new byte[TextureSize * TextureSize * 4];
    private readonly RotateTransform _rotation = new() { CenterX = 0.5, CenterY = 0.5 };
    private readonly Storyboard _spin = new() { RepeatBehavior = RepeatBehavior.Forever };

    private WriteableBitmap? _texture;
    private UIElement? _glow;
    private double _hover;
    private double _hoverFrom;
    private double _hoverTo;
    private long _hoverStart;
    private bool _isAnimatingHover;

    public ShinyButton()
    {
        DefaultStyleKey = typeof(ShinyButton);
        Loaded += (_, _) => _spin.Begin();
        Unloaded += (_, _) =>
        {
            _spin.Stop();
            StopHoverAnimation();
        };
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _glow = GetTemplateChild("Glow") as UIElement;
        if (GetTemplateChild("Ring") is not Border ring)
        {
            return;
        }

        _texture = new WriteableBitmap(TextureSize, TextureSize);
        ring.BorderBrush = new ImageBrush { ImageSource = _texture, Stretch = Stretch.Fill, RelativeTransform = _rotation };
        ApplyHover(_hover);
        CreateSpinAnimation();
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        AnimateHoverTo(1);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        AnimateHoverTo(0);
    }

    private void CreateSpinAnimation()
    {
        var spin = new DoubleAnimation { From = 0, To = 360, Duration = SpinDuration, EnableDependentAnimation = true };
        Storyboard.SetTarget(spin, _rotation);
        Storyboard.SetTargetProperty(spin, nameof(RotateTransform.Angle));

        _spin.Stop();
        _spin.Children.Clear();
        _spin.Children.Add(spin);
    }

    private void AnimateHoverTo(double target)
    {
        _hoverFrom = _hover;
        _hoverTo = target;
        _hoverStart = Stopwatch.GetTimestamp();

        if (!_isAnimatingHover)
        {
            CompositionTarget.Rendering += OnRendering;
            _isAnimatingHover = true;
        }
    }

    private void OnRendering(object? sender, object e)
    {
        var progress = Math.Min(Stopwatch.GetElapsedTime(_hoverStart) / HoverDuration, 1);
        var eased = 1 - Math.Pow(1 - progress, 4);
        ApplyHover(_hoverFrom + (_hoverTo - _hoverFrom) * eased);

        if (progress >= 1)
        {
            StopHoverAnimation();
        }
    }

    private void StopHoverAnimation()
    {
        if (_isAnimatingHover)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isAnimatingHover = false;
        }
    }

    private void ApplyHover(double hover)
    {
        _hover = hover;
        if (_glow is not null)
        {
            _glow.Opacity = hover;
        }

        DrawRing(
            spread: RestSpread + (HoverSpread - RestSpread) * hover,
            shine: Mix(Shine, HighlightSubtle, hover),
            angleShiftDegrees: HoverAngleShift * hover);
    }

    private void DrawRing(double spread, Color shine, double angleShiftDegrees)
    {
        if (_texture is null)
        {
            return;
        }

        const double Center = (TextureSize - 1) / 2.0;
        for (var y = 0; y < TextureSize; y++)
        {
            for (var x = 0; x < TextureSize; x++)
            {
                var bearing = Math.Atan2(x - Center, Center - y) * 180 / Math.PI;
                var turn = ((bearing + angleShiftDegrees) % 360 + 360) % 360 / 360;
                WritePixel((y * TextureSize + x) * 4, ColorAt(turn, spread, shine));
            }
        }

        using (var stream = _texture.PixelBuffer.AsStream())
        {
            stream.Write(_pixels, 0, _pixels.Length);
        }

        _texture.Invalidate();
    }

    // Same stops as the CSS original: transparent, highlight, shine, highlight, transparent.
    private static Color ColorAt(double turn, double spread, Color shine)
    {
        var transparentHighlight = Color.FromArgb(0, Highlight.R, Highlight.G, Highlight.B);
        var position = turn / spread;

        return position switch
        {
            < 1 => Mix(transparentHighlight, Highlight, position),
            < 2 => Mix(Highlight, shine, position - 1),
            < 3 => Mix(shine, Highlight, position - 2),
            < 4 => Mix(Highlight, transparentHighlight, position - 3),
            _ => transparentHighlight,
        };
    }

    private void WritePixel(int offset, Color color)
    {
        // WriteableBitmap expects premultiplied BGRA.
        var alpha = color.A / 255.0;
        _pixels[offset] = (byte)(color.B * alpha);
        _pixels[offset + 1] = (byte)(color.G * alpha);
        _pixels[offset + 2] = (byte)(color.R * alpha);
        _pixels[offset + 3] = color.A;
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        static byte Channel(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);

        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            Channel(from.A, to.A, amount),
            Channel(from.R, to.R, amount),
            Channel(from.G, to.G, amount),
            Channel(from.B, to.B, amount));
    }
}
