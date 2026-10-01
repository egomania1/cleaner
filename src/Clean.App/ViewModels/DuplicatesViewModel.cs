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

public sealed class DuplicatesViewModel : FileToolViewModel
{
    private const int ShownGroupCount = 300;
    private const int FolderRowCount = 8;

    private static readonly long[] MinimumSizes = [1L << 20, 10L << 20, 100L << 20];

    private readonly IDuplicateFinder _finder;
    private readonly IInstalledProgramCatalog _programs;
    private IReadOnlyList<DuplicateGroup> _groups = [];
    private IReadOnlyList<DuplicateGroupRow> _rows = [];
    private IReadOnlyList<ShareRow> _byType = [];
    private IReadOnlyList<ShareRow> _byFolder = [];
    private KeepStrategy _strategy = KeepStrategy.Oldest;
    private long _cloudOnlyCount;

    public DuplicatesViewModel(
        IDiskService diskService,
        IFileScanner scanner,
        IDuplicateFinder finder,
        IInstalledProgramCatalog programs,
        IFileRemover remover,
        IFileExplorer explorer,
        ILicenseService license,
        NavigationService navigation,
        ILogger<DuplicatesViewModel> logger)
        : base(diskService, scanner, remover, explorer, license, navigation, logger)
    {
        _finder = finder;
        _programs = programs;
        KeepOldestCommand = new RelayCommand(() => ApplyStrategy(KeepStrategy.Oldest));
        KeepNewestCommand = new RelayCommand(() => ApplyStrategy(KeepStrategy.Newest));
        KeepShortestCommand = new RelayCommand(() => ApplyStrategy(KeepStrategy.ShortestPath));
        SelectNoneCommand = new RelayCommand(SelectNone);
    }

    public ICommand KeepOldestCommand { get; }

    public ICommand KeepNewestCommand { get; }

    public ICommand KeepShortestCommand { get; }

    public ICommand SelectNoneCommand { get; }

    public override IReadOnlyList<string> SizeOptions { get; } = ["Fichiers de 1 Mo et plus", "Fichiers de 10 Mo et plus", "Fichiers de 100 Mo et plus"];

    public IReadOnlyList<DuplicateGroupRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public IReadOnlyList<ShareRow> ByType
    {
        get => _byType;
        private set => SetProperty(ref _byType, value);
    }

    public IReadOnlyList<ShareRow> ByFolder
    {
        get => _byFolder;
        private set => SetProperty(ref _byFolder, value);
    }

    public bool HasDuplicates => IsReady && _groups.Count > 0;

    public bool HasNoDuplicates => IsReady && _groups.Count == 0;

    public string WastedText => ByteSize.Format(_groups.Sum(group => group.WastedBytes));

    public string GroupCountText => _groups.Count.ToString("N0", French);

    public string ExtraCopiesText => _groups.Sum(group => group.Files.Count - 1).ToString("N0", French);

    public string StrategyText => _strategy switch
    {
        KeepStrategy.Newest => "Garde la copie la plus récente de chaque fichier",
        KeepStrategy.ShortestPath => "Garde la copie au chemin le plus court",
        _ => "Garde la copie la plus ancienne (souvent l'originale)",
    };

    public string HiddenGroupsText => _groups.Count > ShownGroupCount
        ? $"Les {ShownGroupCount} groupes qui prennent le plus de place sont affichés ; {(_groups.Count - ShownGroupCount).ToString("N0", French)} plus petits ne le sont pas."
        : string.Empty;

    public string ScopeText => "Windows, Program Files, les données des applications (AppData) et les dossiers des applications installées ne sont pas examinés : leurs doublons sont voulus."
        + (_cloudOnlyCount > 0 ? $" {_cloudOnlyCount.ToString("N0", French)} fichiers OneDrive non téléchargés ont été ignorés." : string.Empty);

    public override long SelectedBytes => Rows.Sum(row => row.SelectedBytes);

    public override int SelectedCount => Rows.Sum(row => row.SelectedCount);

    protected override string RuleId => "DUPLICATES";

    protected override string Label => "Doublons";

    protected override async Task AnalyzeCoreAsync(DiskItemViewModel disk, CancellationToken cancellationToken)
    {
        PhaseText = "Liste des fichiers…";
        var scan = await Scanner.ScanAsync(disk.Disk.RootPath, MinimumSizes[Math.Clamp(SelectedSizeIndex, 0, MinimumSizes.Length - 1)], new Progress<ScanProgress>(ShowScanProgress), cancellationToken);
        _cloudOnlyCount = scan.CloudOnlyFileCount;

        // Copies inside an application's own folder are part of how it works, never clutter.
        var candidates = await Task.Run(() => scan.Files.Where(file => ProgramMatcher.ProgramsAt(_programs.All, file.Path).Count == 0).ToList(), cancellationToken);

        PhaseText = "Comparaison du contenu…";
        IsProgressIndeterminate = false;
        var progress = new Progress<DuplicateProgress>(report =>
        {
            ProgressRatio = report.BytesToCompare > 0 ? (double)report.BytesCompared / report.BytesToCompare : 0;
            SecondaryProgressText = $"{ByteSize.Format(report.BytesCompared)} / {ByteSize.Format(report.BytesToCompare)}";
            CurrentPath = report.CurrentPath;
        });
        _groups = await _finder.FindAsync(candidates, progress, cancellationToken);

        Rows = _groups.Take(ShownGroupCount).Select(group => new DuplicateGroupRow(group, OnSelectionChanged)).ToList();
        foreach (var row in Rows)
        {
            row.Apply(_strategy);
        }

        BuildCharts();
    }

    protected override IReadOnlyList<RemovalRequest> SelectedRequests() => Rows.SelectMany(row => row.Requests()).ToList();

    protected override string? SelectionWarning() => null;

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(HasNoDuplicates));
        OnPropertyChanged(nameof(WastedText));
        OnPropertyChanged(nameof(GroupCountText));
        OnPropertyChanged(nameof(ExtraCopiesText));
        OnPropertyChanged(nameof(HiddenGroupsText));
        OnPropertyChanged(nameof(ScopeText));
    }

    private void ApplyStrategy(KeepStrategy strategy)
    {
        _strategy = strategy;
        OnPropertyChanged(nameof(StrategyText));
        foreach (var row in Rows)
        {
            row.Apply(strategy);
        }
    }

    private void SelectNone()
    {
        foreach (var row in Rows)
        {
            row.SelectNone();
        }
    }

    private void BuildCharts()
    {
        var byType = _groups
            .GroupBy(group => ChartPalette.ChartCategory(FileCategories.CategoryOf(group.Files[0].Extension)))
            .Select(group => (Label: group.Key, Bytes: group.Sum(item => item.WastedBytes), Count: group.Count()))
            .OrderByDescending(entry => entry.Bytes)
            .ToList();
        var largestType = byType.Select(entry => entry.Bytes).DefaultIfEmpty(0).Max();
        ByType = byType
            .Select(entry => new ShareRow(entry.Label, entry.Bytes, $"{entry.Count} groupe{(entry.Count > 1 ? "s" : string.Empty)}", Ratio(entry.Bytes, largestType), ChartPalette.BrushOfCategory(entry.Label)))
            .ToList();

        // Every extra copy counts where it sits; the kept copy is not clutter.
        var byFolder = _groups
            .SelectMany(group => group.Files
                .Where(file => !ReferenceEquals(file, Keeper.Choose(group, KeepStrategy.Oldest)))
                .Select(file => (file.Folder, group.SizeBytes)))
            .GroupBy(entry => entry.Folder, StringComparer.OrdinalIgnoreCase)
            .Select(group => (Folder: group.Key, Bytes: group.Sum(entry => entry.SizeBytes), Count: group.Count()))
            .OrderByDescending(entry => entry.Bytes)
            .Take(FolderRowCount)
            .ToList();
        var largestFolder = byFolder.Select(entry => entry.Bytes).DefaultIfEmpty(0).Max();
        ByFolder = byFolder
            .Select(entry => new ShareRow(entry.Folder, entry.Bytes, $"{entry.Count} copie{(entry.Count > 1 ? "s" : string.Empty)}", Ratio(entry.Bytes, largestFolder), new(ChartPalette.Series)))
            .ToList();
    }

    private static double Ratio(long value, long largest) => largest > 0 ? (double)value / largest : 0;
}
