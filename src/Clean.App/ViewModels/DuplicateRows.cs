using System.Globalization;
using Clean.App.Controls;
using Clean.Core.Files;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.ViewModels;

public sealed class DuplicateGroupRow
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly Action _selectionChanged;

    public DuplicateGroupRow(DuplicateGroup group, Action selectionChanged)
    {
        Group = group;
        _selectionChanged = selectionChanged;
        Files = group.Files.Select(file => new DuplicateFileRow(file, this)).ToList();
        Category = FileCategories.CategoryOf(group.Files[0].Extension);
    }

    public DuplicateGroup Group { get; }

    public IReadOnlyList<DuplicateFileRow> Files { get; }

    public string Category { get; }

    public string Name => Group.Files[0].Name;

    public SolidColorBrush Brush => ChartPalette.BrushOfCategory(Category);

    public string CopiesText => $"{Files.Count} copies identiques de {ByteSize.Format(Group.SizeBytes)} · {Category}";

    public string WastedText => ByteSize.Format(Group.WastedBytes);

    public long SelectedBytes => Files.Count(file => file.IsSelected) * Group.SizeBytes;

    public int SelectedCount => Files.Count(file => file.IsSelected);

    // At least one copy always stays: the last unticked box cannot be ticked.
    public bool CanSelect(DuplicateFileRow row) => Files.Count(file => file.IsSelected && file != row) < Files.Count - 1;

    public void Apply(KeepStrategy strategy)
    {
        var keeper = Keeper.Choose(Group, strategy);
        foreach (var file in Files)
        {
            file.SetSelected(!ReferenceEquals(file.File, keeper));
        }

        _selectionChanged();
    }

    public void SelectNone()
    {
        foreach (var file in Files)
        {
            file.SetSelected(false);
        }

        _selectionChanged();
    }

    public IEnumerable<RemovalRequest> Requests()
    {
        var kept = Files.First(file => !file.IsSelected).File.Path;
        return Files
            .Where(file => file.IsSelected)
            .Select(file => new RemovalRequest(file.File.Path, file.File.SizeBytes, file.File.LastWriteUtc, kept));
    }

    internal void OnRowChanged() => _selectionChanged();

    internal static string DateOf(FoundFile file) =>
        $"Modifié le {file.LastWriteUtc.ToLocalTime().ToString("d MMMM yyyy", French)}";
}

public sealed class DuplicateFileRow(FoundFile file, DuplicateGroupRow group) : ObservableObject
{
    private bool _isSelected;

    public FoundFile File => file;

    public string Path => file.Path;

    public string Folder => file.Folder;

    public string DateText => DuplicateGroupRow.DateOf(file);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value && !group.CanSelect(this))
            {
                // Refused: raising the change puts the checkbox back.
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(StateText));
                group.OnRowChanged();
            }
        }
    }

    public string StateText => IsSelected ? "RETIRÉE" : "GARDÉE";

    internal void SetSelected(bool value)
    {
        if (SetProperty(ref _isSelected, value, nameof(IsSelected)))
        {
            OnPropertyChanged(nameof(StateText));
        }
    }
}
