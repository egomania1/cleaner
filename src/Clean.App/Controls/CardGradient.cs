using Clean.Core.Storage;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Clean.App.Controls;

public sealed class CardGradient
{
    private const double FullTurn = Math.PI * 2;
    private const double BaseTurnSeconds = 24;

    private static readonly Dictionary<LocationKind, (Color Dark, Color Light)> Palettes = new()
    {
        [LocationKind.System] = (Rgb(0x3B, 0x0A, 0x1E), Rgb(0xB9, 0x1C, 0x1C)),
        [LocationKind.Applications] = (Rgb(0x1E, 0x1B, 0x4B), Rgb(0x7C, 0x3A, 0xED)),
        [LocationKind.UserData] = (Rgb(0x42, 0x20, 0x06), Rgb(0xD9, 0x77, 0x06)),
        [LocationKind.RecycleBin] = (Rgb(0x05, 0x2E, 0x16), Rgb(0x10, 0xB9, 0x81)),
        [LocationKind.SystemFile] = (Rgb(0x1F, 0x29, 0x37), Rgb(0xF9, 0x73, 0x16)),
        [LocationKind.Group] = (Rgb(0x0F, 0x17, 0x2A), Rgb(0x47, 0x55, 0x69)),
        [LocationKind.Folder] = (Rgb(0x0C, 0x4A, 0x6E), Rgb(0x38, 0xBD, 0xF8)),
        [LocationKind.File] = (Rgb(0x16, 0x4E, 0x63), Rgb(0x22, 0xD3, 0xEE)),
    };

    private readonly LinearGradientBrush _base = new();
    private readonly RadialGradientBrush _glow = new() { RadiusX = 0.7, RadiusY = 0.7 };
    private readonly double _anglePhase;
    private readonly double _glowPhaseX;
    private readonly double _glowPhaseY;

    public CardGradient(LocationKind kind, string name)
    {
        var (dark, light) = Palettes[kind];

        _base.GradientStops.Add(new GradientStop { Color = dark, Offset = 0 });
        _base.GradientStops.Add(new GradientStop { Color = Mix(dark, light, 0.55), Offset = 0.6 });
        _base.GradientStops.Add(new GradientStop { Color = light, Offset = 1 });

        _glow.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF), Offset = 0 });
        _glow.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x55, light.R, light.G, light.B), Offset = 0.35 });
        _glow.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, light.R, light.G, light.B), Offset = 1 });

        _anglePhase = Fraction(name, salt: 1) * FullTurn;
        _glowPhaseX = Fraction(name, salt: 2) * FullTurn;
        _glowPhaseY = Fraction(name, salt: 3) * FullTurn;

        Animate(TimeSpan.Zero);
    }

    public Brush Base => _base;

    public Brush Glow => _glow;

    public void Animate(TimeSpan elapsed)
    {
        var seconds = elapsed.TotalSeconds;

        var angle = _anglePhase + seconds * FullTurn / BaseTurnSeconds;
        var direction = new Point(Math.Cos(angle) / 2, Math.Sin(angle) / 2);
        _base.StartPoint = new Point(0.5 - direction.X, 0.5 - direction.Y);
        _base.EndPoint = new Point(0.5 + direction.X, 0.5 + direction.Y);

        // Two unrelated speeds on each axis make the glow wander without visibly looping.
        var center = new Point(
            0.5 + 0.3 * Math.Sin(seconds * 0.45 + _glowPhaseX),
            0.5 + 0.32 * Math.Sin(seconds * 0.31 + _glowPhaseY));
        _glow.Center = center;
        _glow.GradientOrigin = center;
    }

    // string.GetHashCode changes on every launch; a small FNV-1a hash keeps each name's gradient stable.
    private static double Fraction(string text, uint salt)
    {
        var hash = 2166136261u ^ salt;
        foreach (var character in text)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return (hash % 1000) / 1000.0;
    }

    private static Color Mix(Color from, Color to, double amount) => Rgb(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount));

    private static Color Rgb(byte red, byte green, byte blue) => Color.FromArgb(255, red, green, blue);
}
