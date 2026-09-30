using System.Globalization;
using Clean.App.Controls;
using Clean.Core.Files;
using Clean.Core.Formatting;
using Clean.Core.Models;
using Clean.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.ViewModels;

public sealed class LargeFileRow : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly Action _selectionChanged;
    private readonly LocationDescription _description;
    private bool _isSelected;

    public LargeFileRow(FoundFile file, string? ownerApp, long largestBytes, DateTime nowUtc, Action selectionChanged)
    {
        File = file;
        OwnerApp = ownerApp;
        _selectionChanged = selectionChanged;
        _description = LocationGuide.Describe(new StorageUsage(file.Name, file.SizeBytes, file.Path));
        Category = FileCategories.CategoryOf(file.Extension);
        AgeBucket = AgeBuckets.IndexOf(file.LastWriteUtc, nowUtc);
        Ratio = largestBytes > 0 ? (double)file.SizeBytes / largestBytes : 0;
        AgeText = $"Modifié {RelativeDate.Ago(new DateTimeOffset(file.LastWriteUtc, TimeSpan.Zero).ToLocalTime(), new DateTimeOffset(nowUtc, TimeSpan.Zero).ToLocalTime())}";
    }

    public FoundFile File { get; }

    public string? OwnerApp { get; }

    public string Category { get; }

    public int AgeBucket { get; }

    public double Ratio { get; }

    public string Name => File.Name;

    public string Folder => File.Folder;

    public string Path => File.Path;

    public string SizeText => ByteSize.Format(File.SizeBytes);

    public string AgeText { get; }

    public SolidColorBrush Brush => ChartPalette.BrushOfCategory(Category);

    public GridLength BarWidth => new(Math.Max(Ratio, 0.002), GridUnitType.Star);

    public GridLength BarRest => new(Math.Max(1 - Ratio, 0.0001), GridUnitType.Star);

    // Files of an installed application are shown, since they take space, but never offered for removal.
    public bool CanSelect => OwnerApp is null;

    public string ProtectionText => OwnerApp is null ? string.Empty : $"Fait partie de {OwnerApp} : désinstalle l'application pour libérer cette place.";

    public bool IsProtected => OwnerApp is not null;

    public string Summary => _description.Summary;

    public string Advice => OwnerApp is null ? _description.Advice : ProtectionText;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value && CanSelect))
            {
                _selectionChanged();
            }
        }
    }

    public string InfoText => $"{Category} · {AgeText}";

    internal void SetSelected(bool value)
    {
        if (SetProperty(ref _isSelected, value && CanSelect, nameof(IsSelected)))
        {
            _selectionChanged();
        }
    }

    public static string CountText(int count) => $"{count.ToString("N0", French)} fichier{(count > 1 ? "s" : string.Empty)}";
}
