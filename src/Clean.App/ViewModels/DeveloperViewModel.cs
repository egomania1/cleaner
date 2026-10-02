using System.Globalization;
using System.Windows.Input;
using Clean.App.Controls;
using Clean.App.Services;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

public sealed class DeveloperViewModel : FileToolViewModel
{
    private const int ShownRowCount = 500;

    private static readonly long[] MinimumSizes = [0, 50L << 20, 500L << 20];

    private readonly IDeveloperScanner _scanner;
    private readonly TimeProvider _clock;
    private IReadOnlyList<DevFolderRow> _all = [];
    private IReadOnlyList<ShareRow> _byKind = [];

    public DeveloperViewModel(
        IDiskService diskService,
        IDeveloperScanner scanner,
        IFileRemover remover,
        IFileExplorer explorer,
        ILicenseService license,
        TimeProvider clock,
        NavigationService navigation,
        ILogger<DeveloperViewModel> logger)
        : base(diskService, remover, explorer, license, navigation, logger)
    {
        _scanner = scanner;
        _clock = clock;
        SelectNoneCommand = new RelayCommand(() => SetSelection(_ => false));
        SelectInactiveCommand = new RelayCommand(() => SetSelection(row => !row.IsActive));
    }

    public ICommand SelectNoneCommand { get; }

    public ICommand SelectInactiveCommand { get; }

    public override IReadOnlyList<string> SizeOptions { get; } = ["Tous les dossiers", "Dossiers de 50 Mo et plus", "Dossiers de 500 Mo et plus"];

    public IReadOnlyList<DevFolderRow> Rows => _all.Take(ShownRowCount).ToList();

    public IReadOnlyList<ShareRow> ByKind
    {
        get => _byKind;
        private set => SetProperty(ref _byKind, value);
    }

    public bool HasFolders => IsReady && _all.Count > 0;

    public bool HasNoFolders => IsReady && _all.Count == 0;

    public string CountText => _all.Count.ToString("N0", French);

    public string TotalText => ByteSize.Format(_all.Sum(row => row.Folder.SizeBytes));

    public string InactiveText => ByteSize.Format(_all.Where(row => !row.IsActive).Sum(row => row.Folder.SizeBytes));

    public string ListCountText => _all.Count > ShownRowCount
        ? $"Les {ShownRowCount} plus gros sur {_all.Count.ToString("N0", French)}"
        : $"{_all.Count.ToString("N0", French)} dossier{(_all.Count > 1 ? "s" : string.Empty)}";

    public override long SelectedBytes => _all.Where(row => row.IsSelected).Sum(row => row.Folder.SizeBytes);

    public override int SelectedCount => _all.Where(row => row.IsSelected).Sum(row => row.Folder.Files.Count);

    protected override string RuleId => "DEVELOPER_FOLDERS";

    protected override string Label => "Dossiers de développement";

    protected override async Task AnalyzeCoreAsync(DiskItemViewModel disk, CancellationToken cancellationToken)
    {
        PhaseText = "Recherche des dossiers de développement…";
        var progress = new Progress<Core.Models.ScanProgress>(report =>
        {
            ShowScanProgress(report);
            SecondaryProgressText = ByteSize.Format(report.BytesAnalyzed);
        });
        var scan = await _scanner.ScanAsync(disk.Disk.RootPath, progress, cancellationToken);

        var minimum = MinimumSizes[Math.Clamp(SelectedSizeIndex, 0, MinimumSizes.Length - 1)];
        var folders = scan.Folders.Where(folder => folder.SizeBytes >= minimum).OrderByDescending(folder => folder.SizeBytes).ToList();
        var now = _clock.GetUtcNow().UtcDateTime;
        var largest = folders.Select(folder => folder.SizeBytes).DefaultIfEmpty(0).Max();

        _all = folders.Select(folder => new DevFolderRow(folder, largest, now, OnSelectionChanged)).ToList();
        OnPropertyChanged(nameof(Rows));
        BuildChart();
    }

    protected override IReadOnlyList<RemovalRequest> SelectedRequests() =>
        _all.Where(row => row.IsSelected)
            .SelectMany(row => row.Folder.Files)
            .Select(file => new RemovalRequest(file.Path, file.SizeBytes, file.LastWriteUtc))
            .ToList();

    protected override string? SelectionWarning() =>
        _all.Any(row => row.IsSelected && row.IsActive)
            ? "Certains de ces projets ont été modifiés ces 30 derniers jours. Leur compilation ou leurs paquets seront à refaire au prochain lancement."
            : null;

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(HasFolders));
        OnPropertyChanged(nameof(HasNoFolders));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(InactiveText));
        OnPropertyChanged(nameof(ListCountText));
    }

    private void BuildChart()
    {
        var byKind = _all
            .GroupBy(row => row.Folder.Kind.Title)
            .Select(group => (Title: group.Key, Bytes: group.Sum(row => row.Folder.SizeBytes), Count: group.Count()))
            .OrderByDescending(entry => entry.Bytes)
            .ToList();
        var largest = byKind.Select(entry => entry.Bytes).DefaultIfEmpty(0).Max();
        var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(ChartPalette.Series);
        ByKind = byKind
            .Select(entry => new ShareRow(entry.Title, entry.Bytes, $"{entry.Count.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} dossier{(entry.Count > 1 ? "s" : string.Empty)}", largest > 0 ? (double)entry.Bytes / largest : 0, brush))
            .ToList();
    }

    private void SetSelection(Func<DevFolderRow, bool> predicate)
    {
        foreach (var row in _all)
        {
            row.SetSelected(predicate(row));
        }
    }
}
