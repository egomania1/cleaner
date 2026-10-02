using Clean.App.Controls;
using Clean.Core.Developer;
using Clean.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.ViewModels;

public sealed class DevFolderRow : ObservableObject
{
    // A project whose files changed this recently is probably being worked on.
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromDays(30);

    private static readonly SolidColorBrush Brush = new(ChartPalette.Series);

    private readonly Action _selectionChanged;
    private bool _isSelected;

    public DevFolderRow(DevFolder folder, long largestBytes, DateTime nowUtc, Action selectionChanged)
    {
        Folder = folder;
        _selectionChanged = selectionChanged;
        Ratio = largestBytes > 0 ? (double)folder.SizeBytes / largestBytes : 0;
        IsActive = nowUtc - folder.LastWriteUtc < ActiveWindow;
        AgeText = $"Touché {RelativeDate.Ago(new DateTimeOffset(folder.LastWriteUtc, TimeSpan.Zero).ToLocalTime(), new DateTimeOffset(nowUtc, TimeSpan.Zero).ToLocalTime())}";
    }

    public DevFolder Folder { get; }

    public double Ratio { get; }

    public bool IsActive { get; }

    public string AgeText { get; }

    public string ProjectName => Folder.ProjectName;

    public string Path => Folder.Path;

    public string KindTitle => Folder.Kind.Title;

    public string Explanation => Folder.Kind.Explanation;

    public string RestoreText => $"Pour le retrouver : {Folder.Kind.HowToRestore}";

    public string SizeText => ByteSize.Format(Folder.SizeBytes);

    public string FileCountText => $"{Folder.Files.Count:N0} fichiers";

    public string ActiveText => IsActive ? "Projet utilisé ces 30 derniers jours" : string.Empty;

    public SolidColorBrush BarBrush => Brush;

    public GridLength BarWidth => new(Math.Max(Ratio, 0.002), GridUnitType.Star);

    public GridLength BarRest => new(Math.Max(1 - Ratio, 0.0001), GridUnitType.Star);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged();
            }
        }
    }

    internal void SetSelected(bool value)
    {
        if (SetProperty(ref _isSelected, value, nameof(IsSelected)))
        {
            _selectionChanged();
        }
    }
}
