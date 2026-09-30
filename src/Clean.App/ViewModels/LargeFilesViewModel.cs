using System.Windows.Input;
using Clean.App.Controls;
using Clean.App.Services;
using Clean.Core.Files;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Storage;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

public sealed class LargeFilesViewModel : FileToolViewModel
{
    private const int TreemapTileCount = 40;
    private const int ShownRowCount = 400;
    private const string AllCategories = "Toutes les catégories";

    private static readonly long[] MinimumSizes = [100L << 20, 500L << 20, 1L << 30];

    private readonly IInstalledProgramCatalog _programs;
    private readonly TimeProvider _clock;
    private IReadOnlyList<LargeFileRow> _all = [];
    private IReadOnlyList<LargeFileRow> _filtered = [];
    private IReadOnlyList<TreemapTile> _tiles = [];
    private IReadOnlyList<ShareRow> _byCategory = [];
    private IReadOnlyList<ShareRow> _byAge = [];
    private IReadOnlyList<string> _categoryOptions = [AllCategories];
    private int _categoryIndex;
    private int _sortIndex;
    private LargeFileRow? _focused;

    public LargeFilesViewModel(
        IDiskService diskService,
        IFileScanner scanner,
        IInstalledProgramCatalog programs,
        IFileRemover remover,
        IFileExplorer explorer,
        TimeProvider clock,
        NavigationService navigation,
        ILogger<LargeFilesViewModel> logger)
        : base(diskService, scanner, remover, explorer, navigation, logger)
    {
        _programs = programs;
        _clock = clock;
        SelectNoneCommand = new RelayCommand(() => SetSelection(_ => false));
        SelectStaleCommand = new RelayCommand(() => SetSelection(row => row.AgeBucket >= 3));
        RevealFocusedCommand = new RelayCommand(() => Reveal(Focused!.Path), () => Focused is not null);
        ToggleFocusedCommand = new RelayCommand(() => Focused!.IsSelected = !Focused.IsSelected, () => Focused?.CanSelect == true);
    }

    public ICommand SelectNoneCommand { get; }

    public ICommand SelectStaleCommand { get; }

    public ICommand RevealFocusedCommand { get; }

    public ICommand ToggleFocusedCommand { get; }

    public override IReadOnlyList<string> SizeOptions { get; } = ["Fichiers de 100 Mo et plus", "Fichiers de 500 Mo et plus", "Fichiers de 1 Go et plus"];

    public IReadOnlyList<string> SortOptions { get; } = ["Les plus gros", "Les plus anciens", "Les plus récents", "Par nom"];

    public IReadOnlyList<string> CategoryOptions
    {
        get => _categoryOptions;
        private set => SetProperty(ref _categoryOptions, value);
    }

    public int CategoryIndex
    {
        get => _categoryIndex;
        set
        {
            if (SetProperty(ref _categoryIndex, value))
            {
                RefreshFilter();
            }
        }
    }

    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (SetProperty(ref _sortIndex, value))
            {
                RefreshFilter();
            }
        }
    }

    public IReadOnlyList<LargeFileRow> Filtered
    {
        get => _filtered;
        private set
        {
            if (SetProperty(ref _filtered, value))
            {
                OnPropertyChanged(nameof(FilteredCountText));
            }
        }
    }

    public string FilteredCountText => Filtered.Count >= ShownRowCount
        ? $"Les {ShownRowCount} premiers sur {_all.Count.ToString("N0", French)}"
        : LargeFileRow.CountText(Filtered.Count);

    public IReadOnlyList<TreemapTile> Tiles
    {
        get => _tiles;
        private set => SetProperty(ref _tiles, value);
    }

    public IReadOnlyList<ShareRow> ByCategory
    {
        get => _byCategory;
        private set => SetProperty(ref _byCategory, value);
    }

    public IReadOnlyList<ShareRow> ByAge
    {
        get => _byAge;
        private set => SetProperty(ref _byAge, value);
    }

    public LargeFileRow? Focused
    {
        get => _focused;
        private set
        {
            if (SetProperty(ref _focused, value))
            {
                OnPropertyChanged(nameof(HasFocus));
                OnPropertyChanged(nameof(HasNoFocus));
                OnPropertyChanged(nameof(FocusedKey));
                ((RelayCommand)RevealFocusedCommand).NotifyCanExecuteChanged();
                ((RelayCommand)ToggleFocusedCommand).NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasFocus => Focused is not null;

    public bool HasNoFocus => Focused is null;

    public string? FocusedKey => Focused?.Path;

    public bool HasFiles => IsReady && _all.Count > 0;

    public bool HasNoFiles => IsReady && _all.Count == 0;

    public string CountText => _all.Count.ToString("N0", French);

    public string TotalText => ByteSize.Format(_all.Sum(row => row.File.SizeBytes));

    public string BiggestText => _all.Count > 0 ? ByteSize.Format(_all.Max(row => row.File.SizeBytes)) : "—";

    public string BiggestName => _all.OrderByDescending(row => row.File.SizeBytes).FirstOrDefault()?.Name ?? string.Empty;

    public string StaleText => ByteSize.Format(_all.Where(row => row.AgeBucket >= 3).Sum(row => row.File.SizeBytes));

    public string ProtectedText
    {
        get
        {
            var count = _all.Count(row => row.IsProtected);
            return count == 0
                ? "Windows, Program Files et AppData ne sont pas examinés."
                : $"Windows, Program Files et AppData ne sont pas examinés. {count.ToString("N0", French)} fichier(s) d'applications installées sont affichés mais ne peuvent pas être retirés ici.";
        }
    }

    public override long SelectedBytes => _all.Where(row => row.IsSelected).Sum(row => row.File.SizeBytes);

    public override int SelectedCount => _all.Count(row => row.IsSelected);

    protected override string RuleId => "LARGE_FILES";

    protected override string Label => "Gros fichiers";

    public void Focus(string? path) => Focused = _all.FirstOrDefault(row => row.Path == path);

    protected override async Task AnalyzeCoreAsync(DiskItemViewModel disk, CancellationToken cancellationToken)
    {
        PhaseText = "Recherche des gros fichiers…";
        var progress = new Progress<ScanProgress>(report =>
        {
            ShowScanProgress(report);
            SecondaryProgressText = ByteSize.Format(report.BytesAnalyzed);
        });
        var scan = await Scanner.ScanAsync(disk.Disk.RootPath, MinimumSizes[Math.Clamp(SelectedSizeIndex, 0, MinimumSizes.Length - 1)], progress, cancellationToken);

        var now = _clock.GetUtcNow().UtcDateTime;
        var largest = scan.Files.Select(file => file.SizeBytes).DefaultIfEmpty(0).Max();
        var rows = await Task.Run(
            () => scan.Files
                .Select(file => new LargeFileRow(file, ProgramMatcher.ProgramsAt(_programs.All, file.Path).FirstOrDefault()?.Name, largest, now, OnSelectionChanged))
                .OrderByDescending(row => row.File.SizeBytes)
                .ToList(),
            cancellationToken);

        _all = rows;
        Focused = null;
        CategoryOptions = [AllCategories, .. rows.Select(row => row.Category).Distinct().Order()];
        _categoryIndex = 0;
        OnPropertyChanged(nameof(CategoryIndex));
        BuildCharts(now);
        RefreshFilter();
    }

    protected override IReadOnlyList<RemovalRequest> SelectedRequests() =>
        _all.Where(row => row.IsSelected).Select(row => new RemovalRequest(row.Path, row.File.SizeBytes, row.File.LastWriteUtc)).ToList();

    // Unlike duplicates, a large file has no other copy: the user must know what they are removing.
    protected override string? SelectionWarning() =>
        "Ces fichiers n'ont pas d'autre copie. Vérifie que tu n'en as plus besoin : après 7 jours, ils seront supprimés pour de bon.";

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasNoFiles));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(BiggestText));
        OnPropertyChanged(nameof(BiggestName));
        OnPropertyChanged(nameof(StaleText));
        OnPropertyChanged(nameof(ProtectedText));
    }

    private void BuildCharts(DateTime now)
    {
        Tiles = _all
            .Take(TreemapTileCount)
            .Select(row => new TreemapTile(row.Path, row.Name, $"{row.Category} · {row.AgeText.ToLowerInvariant()}", row.SizeText, row.File.SizeBytes, ChartPalette.OfCategory(row.Category)))
            .ToList();

        var byCategory = _all
            .GroupBy(row => ChartPalette.ChartCategory(row.Category))
            .Select(group => (Label: group.Key, Bytes: group.Sum(row => row.File.SizeBytes), Count: group.Count()))
            .OrderByDescending(entry => entry.Bytes)
            .ToList();
        var largestCategory = byCategory.Select(entry => entry.Bytes).DefaultIfEmpty(0).Max();
        ByCategory = byCategory
            .Select(entry => new ShareRow(entry.Label, entry.Bytes, LargeFileRow.CountText(entry.Count), Ratio(entry.Bytes, largestCategory), ChartPalette.BrushOfCategory(entry.Label)))
            .ToList();

        // Ages keep their natural order, from recent to old: the order is the information.
        var byAge = Enumerable.Range(0, AgeBuckets.Labels.Count)
            .Select(bucket => (Bucket: bucket, Bytes: _all.Where(row => row.AgeBucket == bucket).Sum(row => row.File.SizeBytes), Count: _all.Count(row => row.AgeBucket == bucket)))
            .ToList();
        var largestAge = byAge.Select(entry => entry.Bytes).DefaultIfEmpty(0).Max();
        ByAge = byAge
            .Select(entry => new ShareRow(AgeBuckets.Labels[entry.Bucket], entry.Bytes, LargeFileRow.CountText(entry.Count), Ratio(entry.Bytes, largestAge), ChartPalette.BrushOfAge(entry.Bucket)))
            .ToList();
    }

    private void RefreshFilter()
    {
        var category = CategoryIndex > 0 && CategoryIndex < CategoryOptions.Count ? CategoryOptions[CategoryIndex] : null;
        var matches = _all.Where(row => category is null || row.Category == category);
        Filtered = (SortIndex switch
        {
            1 => matches.OrderBy(row => row.File.LastWriteUtc),
            2 => matches.OrderByDescending(row => row.File.LastWriteUtc),
            3 => matches.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => matches.OrderByDescending(row => row.File.SizeBytes),
        }).Take(ShownRowCount).ToList();
    }

    private void SetSelection(Func<LargeFileRow, bool> predicate)
    {
        foreach (var row in _all)
        {
            row.SetSelected(predicate(row));
        }
    }

    private static double Ratio(long value, long largest) => largest > 0 ? (double)value / largest : 0;
}
