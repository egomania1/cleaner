using Clean.Core.Apps;
using Clean.Core.Storage;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Clean.App.Controls;

// Validated for the dark surface (lightness band, colorblind separation, 3:1 contrast) with the dataviz checker.
// Slot order is part of the validation: a group keeps its slot, never a rank-based color.
public static class ChartPalette
{
    private static readonly Color[] GroupColors =
    [
        Rgb(0x39, 0x87, 0xE5),
        Rgb(0xD9, 0x59, 0x26),
        Rgb(0x19, 0x9E, 0x70),
        Rgb(0xC9, 0x85, 0x00),
        Rgb(0xD5, 0x51, 0x81),
        Rgb(0x00, 0x83, 0x00),
    ];

    public static readonly Color Other = Rgb(0x6B, 0x6A, 0x66);
    public static readonly Color Series = Rgb(0x39, 0x87, 0xE5);
    public static readonly Color Grid = Rgb(0x2C, 0x2C, 0x2A);
    public static readonly Color Axis = Rgb(0x89, 0x87, 0x81);
    public static readonly Color Surface = Rgb(0x1C, 0x1C, 0x1C);
    public static readonly Color Ink = Rgb(0xFA, 0xFA, 0xFA);

    // File categories get the same eight slots, in the same validated order: the eight that weigh most
    // in large files and duplicates. The others are folded into one gray "Autres fichiers" entry.
    private static readonly Dictionary<string, Color> CategoryColors = new()
    {
        ["Vidéos"] = Rgb(0x39, 0x87, 0xE5),
        ["Images"] = Rgb(0xD9, 0x59, 0x26),
        ["Archives"] = Rgb(0x19, 0x9E, 0x70),
        ["Documents"] = Rgb(0xC9, 0x85, 0x00),
        ["Images disque"] = Rgb(0xD5, 0x51, 0x81),
        ["Programmes"] = Rgb(0x00, 0x83, 0x00),
        ["Journaux et rapports"] = Rgb(0x90, 0x85, 0xE9),
        ["Données (jeux, bases…)"] = Rgb(0xE6, 0x67, 0x67),
    };

    // The label a chart shows for a category, so that every gray mark shares one legend entry.
    public static string ChartCategory(string category) => CategoryColors.ContainsKey(category) ? category : FileCategories.Other;

    // Ordinal ramp for ages, one hue: the older a file, the brighter its bar on the dark surface.
    private static readonly Color[] AgeColors =
    [
        Rgb(0x18, 0x4F, 0x95),
        Rgb(0x25, 0x6A, 0xBF),
        Rgb(0x39, 0x87, 0xE5),
        Rgb(0x6D, 0xA7, 0xEC),
        Rgb(0x9E, 0xC5, 0xF4),
    ];

    public static Color Of(AppGroup group) => (int)group < GroupColors.Length ? GroupColors[(int)group] : Other;

    public static Color OfCategory(string category) => CategoryColors.TryGetValue(category, out var color) ? color : Other;

    public static SolidColorBrush BrushOfCategory(string category) => new(OfCategory(category));

    public static SolidColorBrush BrushOfAge(int bucket) => new(AgeColors[Math.Clamp(bucket, 0, AgeColors.Length - 1)]);

    public static SolidColorBrush BrushOf(AppGroup group) => new(Of(group));

    private static Color Rgb(byte red, byte green, byte blue) => Color.FromArgb(255, red, green, blue);
}
