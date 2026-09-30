using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Clean.App.Services;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

public sealed class CleanerViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IScanManager _scanManager;
    private readonly IDiskService _diskService;
    private readonly ISafetyEngine _safetyEngine;
    private readonly ICleaner _cleaner;
    private readonly ILogger<CleanerViewModel> _logger;

    private Task? _disksLoading;
    private int _selectedDiskIndex = -1;
    private string _resultDiskName = string.Empty;
    private bool _resultIsSystemDrive;
    private CleanerState _state = CleanerState.Idle;
    private string _filesScannedText = "0";
    private string _dataAnalyzedText = ByteSize.Format(0);
    private string _currentPath = string.Empty;
    private string _cleaningFilesText = "0";
    private string _cleaningFreedText = ByteSize.Format(0);
    private string? _statusMessage;
    private ScanResult _result = ScanResult.Empty;
    private CleaningResult? _cleaningResult;
    private IReadOnlyList<ScanItemRow> _items = [];

    public CleanerViewModel(
        IScanManager scanManager,
        IDiskService diskService,
        ISafetyEngine safetyEngine,
        ICleaner cleaner,
        NavigationService navigation,
        ILogger<CleanerViewModel> logger)
    {
        _scanManager = scanManager;
        _diskService = diskService;
        _safetyEngine = safetyEngine;
        _cleaner = cleaner;
        _logger = logger;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => SelectedDisk is not null && !IsCleaning);
        CancelCommand = AnalyzeCommand.CreateCancelCommand();
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => IsReady && SelectedRows.Any());
        CancelCleaningCommand = CleanCommand.CreateCancelCommand();
        SelectAllCommand = new RelayCommand(() => SetSelection(_ => true));
        SelectSafeCommand = new RelayCommand(() => SetSelection(row => row.Risk == RiskLevel.Safe));
        SelectNoneCommand = new RelayCommand(() => SetSelection(_ => false));
        ShowHistoryCommand = new RelayCommand(() => navigation.NavigateTo("history"));
    }

    public IAsyncRelayCommand AnalyzeCommand { get; }

    public ICommand CancelCommand { get; }

    public IAsyncRelayCommand CleanCommand { get; }

    public ICommand CancelCleaningCommand { get; }

    public ICommand SelectAllCommand { get; }

    public ICommand SelectSafeCommand { get; }

    public ICommand SelectNoneCommand { get; }

    public ICommand ShowHistoryCommand { get; }

    // Set by the page, which owns the dialog; without an explicit yes nothing is ever deleted.
    public Func<CleaningConfirmation, Task<bool>>? ConfirmAsync { get; set; }

    public ObservableCollection<DiskItemViewModel> Disks { get; } = [];

    public int SelectedDiskIndex
    {
        get => _selectedDiskIndex;
        set
        {
            if (SetProperty(ref _selectedDiskIndex, value))
            {
                AnalyzeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanChangeDisk => !IsScanning && !IsCleaning;

    public bool IsIdle => _state == CleanerState.Idle;

    public bool IsScanning => _state == CleanerState.Scanning;

    public bool IsReady => _state == CleanerState.Ready;

    public bool IsCleaning => _state == CleanerState.Cleaning;

    public bool IsCleaned => _state == CleanerState.Cleaned;

    public bool HasResult => IsReady;

    public string FilesScannedText
    {
        get => _filesScannedText;
        private set => SetProperty(ref _filesScannedText, value);
    }

    public string DataAnalyzedText
    {
        get => _dataAnalyzedText;
        private set => SetProperty(ref _dataAnalyzedText, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public string CleaningFilesText
    {
        get => _cleaningFilesText;
        private set => SetProperty(ref _cleaningFilesText, value);
    }

    public string CleaningFreedText
    {
        get => _cleaningFreedText;
        private set => SetProperty(ref _cleaningFreedText, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => StatusMessage is not null;

    public IReadOnlyList<ScanItemRow> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public string CleanableText => ByteSize.Format(_result.CleanableBytes);

    public string CleanableSummary =>
        $"{_resultDiskName} — {_result.CleanableFileCount.ToString("N0", French)} fichiers dans {_result.Items.Count(item => item.CanClean)} emplacements, analysés en {_result.Duration.TotalSeconds.ToString("0.#", French)} s";

    public bool HasCleanableItems => HasResult && _result.Items.Any(item => item.CanClean);

    public string SelectionText
    {
        get
        {
            var selected = SelectedRows.ToList();
            return selected.Count == 0
                ? "Aucun emplacement sélectionné."
                : $"{ByteSize.Format(selected.Sum(row => row.Item.SizeBytes))} sélectionnés — {selected.Sum(row => row.Item.FileCount).ToString("N0", French)} fichiers dans {selected.Count} emplacement(s)";
        }
    }

    public string CleanButtonText => $"Nettoyer {ByteSize.Format(SelectedRows.Sum(row => row.Item.SizeBytes))}";

    // The rules only know Windows, user profile and developer folders for now, and those live on the system drive.
    public bool HasNothingFound => HasResult && _result.Items.Count == 0;

    public string NothingFoundText => _resultIsSystemDrive
        ? $"Rien à nettoyer sur {_resultDiskName} pour l'instant."
        : $"Rien trouvé sur {_resultDiskName}. Les règles actuelles visent surtout des dossiers de Windows et de ton compte, qui sont sur le disque système.";

    public string FreedText => ByteSize.Format(_cleaningResult?.RemovedBytes ?? 0);

    public string CleanedSummary
    {
        get
        {
            if (_cleaningResult is not { } result)
            {
                return string.Empty;
            }

            var summary = $"{_resultDiskName} — {result.RemovedFileCount.ToString("N0", French)} fichiers retirés de {result.CleanedLocationCount} emplacement(s), en {result.Duration.TotalSeconds.ToString("0.#", French)} s.";
            return result.WasCancelled ? $"Nettoyage arrêté en cours de route. {summary}" : summary;
        }
    }

    public string ArchiveText =>
        $"Ces fichiers sont mis de côté pendant {CleaningSession.RetentionPeriod.Days} jours : tu peux les restaurer depuis l'Historique. " +
        "L'espace est rendu au disque à la fin de ce délai, ou tout de suite avec « Libérer maintenant » dans l'Historique.";

    public bool HasLockedFiles => _cleaningResult?.LockedFileCount > 0;

    public string LockedText =>
        $"{(_cleaningResult?.LockedFileCount ?? 0).ToString("N0", French)} fichiers gardés : ils sont ouverts par une application en cours, ou protégés par Windows (droits administrateur). Ferme tes applications et relance un nettoyage pour en récupérer une partie.";

    public bool HasKeptRecentFiles => _cleaningResult?.KeptRecentFileCount > 0;

    public string KeptRecentText =>
        $"{(_cleaningResult?.KeptRecentFileCount ?? 0).ToString("N0", French)} fichiers modifiés depuis l'analyse ont été gardés, ils servent de nouveau.";

    public string DashboardValue => _state switch
    {
        CleanerState.Cleaned => FreedText,
        CleanerState.Ready or CleanerState.Cleaning => CleanableText,
        _ => "—",
    };

    public string DashboardCaption => _state switch
    {
        CleanerState.Cleaned => "Retiré au dernier nettoyage",
        CleanerState.Ready or CleanerState.Cleaning => "Récupérable, prêt à nettoyer",
        _ => "Pas encore analysé",
    };

    private IEnumerable<ScanItemRow> SelectedRows => Items.Where(row => row.IsSelected);

    private DiskItemViewModel? SelectedDisk =>
        SelectedDiskIndex >= 0 && SelectedDiskIndex < Disks.Count ? Disks[SelectedDiskIndex] : null;

    public Task EnsureDisksLoadedAsync() => _disksLoading ??= LoadDisksAsync();

    private async Task LoadDisksAsync()
    {
        try
        {
            var previous = SelectedDisk?.Disk.RootPath;
            var disks = await _diskService.GetDisksAsync(CancellationToken.None);

            Disks.Clear();
            foreach (var disk in disks)
            {
                Disks.Add(new DiskItemViewModel(disk));
            }

            var selected = Disks.FirstOrDefault(disk => disk.Disk.RootPath == previous) ?? Disks.FirstOrDefault(disk => disk.IsSystemDrive);
            SelectedDiskIndex = selected is not null ? Disks.IndexOf(selected) : Disks.Count > 0 ? 0 : -1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not list the disks");
            StatusMessage = "Impossible de lister les disques.";
        }
    }

    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        if (SelectedDisk is not { } selected)
        {
            return;
        }

        StatusMessage = null;
        FilesScannedText = "0";
        DataAnalyzedText = ByteSize.Format(0);
        CurrentPath = string.Empty;
        SetState(CleanerState.Scanning);

        var progress = new Progress<ScanProgress>(ShowProgress);
        try
        {
            _result = await _scanManager.RunAsync(selected.Disk.RootPath, progress, cancellationToken);
            _resultDiskName = selected.Name;
            _resultIsSystemDrive = selected.IsSystemDrive;
            Items = _result.Items
                .OrderByDescending(item => item.SizeBytes)
                .Select(item => new ScanItemRow(item, OnSelectionChanged))
                .ToList();

            if (_result.Errors.Count > 0)
            {
                StatusMessage = $"{_result.Errors.Count} analyse(s) n'ont pas pu aller au bout : {string.Join(" ; ", _result.Errors.Select(error => error.Message))}";
            }

            SetState(CleanerState.Ready);
            OnSelectionChanged();
        }
        catch (OperationCanceledException)
        {
            SetState(CleanerState.Idle);
            StatusMessage = "Analyse annulée.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Cleanup analysis failed");
            SetState(CleanerState.Idle);
            StatusMessage = "L'analyse a échoué : un emplacement n'était pas accessible.";
        }
    }

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        var decisions = SelectedRows.Select(row => _safetyEngine.Evaluate(row.Item)).ToList();
        var allowed = decisions.Where(decision => decision.IsAllowed).ToList();
        var refused = decisions.Where(decision => !decision.IsAllowed).ToList();

        if (allowed.Count == 0)
        {
            StatusMessage = $"Rien n'a été supprimé : {DescribeRefused(refused)}";
            return;
        }

        if (ConfirmAsync is null || !await ConfirmAsync(BuildConfirmation(allowed, refused)))
        {
            return;
        }

        StatusMessage = null;
        CleaningFilesText = "0";
        CleaningFreedText = ByteSize.Format(0);
        CurrentPath = string.Empty;
        SetState(CleanerState.Cleaning);

        try
        {
            _cleaningResult = await _cleaner.CleanAsync(allowed, new Progress<CleaningProgress>(ShowCleaningProgress), cancellationToken);
            _logger.LogInformation(
                "Cleaned {Bytes} bytes: {Deleted} files deleted, {Locked} locked, {Recent} recent kept",
                _cleaningResult.RemovedBytes,
                _cleaningResult.RemovedFileCount,
                _cleaningResult.LockedFileCount,
                _cleaningResult.KeptRecentFileCount);
            SetState(CleanerState.Cleaned);

            // Free space changed, so the disk picker is refreshed.
            _disksLoading = LoadDisksAsync();
            await _disksLoading;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Cleaning failed");
            SetState(CleanerState.Ready);
            StatusMessage = "Le nettoyage a échoué. Relance l'analyse pour voir ce qui reste.";
        }
    }

    private static CleaningConfirmation BuildConfirmation(IReadOnlyList<CleaningDecision> allowed, IReadOnlyList<CleaningDecision> refused)
    {
        var bytes = allowed.Sum(decision => decision.Item.SizeBytes);
        var files = allowed.Sum(decision => decision.Item.FileCount);
        var message =
            $"{ByteSize.Format(bytes)} ({files.ToString("N0", French)} fichiers) vont être retirés de {allowed.Count} emplacement(s). " +
            $"Ils restent restaurables pendant {CleaningSession.RetentionPeriod.Days} jours depuis l'Historique, puis l'espace est libéré pour de bon. " +
            "Les fichiers ouverts par une application sont gardés.";

        var warnings = new List<string>();
        var risky = allowed.Where(decision => decision.Item.Risk != RiskLevel.Safe).ToList();
        if (risky.Count > 0)
        {
            warnings.Add($"Ta sélection contient des éléments à manier avec prudence : {string.Join(", ", risky.Select(decision => decision.Item.Name))}. Relis leur explication avant de continuer.");
        }

        if (refused.Count > 0)
        {
            warnings.Add($"Ignorés par sécurité : {DescribeRefused(refused)}");
        }

        return new CleaningConfirmation(
            "Nettoyer maintenant ?",
            message,
            warnings.Count > 0 ? string.Join(Environment.NewLine + Environment.NewLine, warnings) : null,
            $"Supprimer {ByteSize.Format(bytes)}");
    }

    private static string DescribeRefused(IEnumerable<CleaningDecision> refused) =>
        string.Join(" ; ", refused.Select(decision => $"{decision.Item.Name} ({decision.Reason})"));

    private void ShowProgress(ScanProgress report)
    {
        if (!IsScanning)
        {
            return;
        }

        FilesScannedText = report.FilesScanned.ToString("N0", French);
        DataAnalyzedText = ByteSize.Format(report.BytesAnalyzed);
        CurrentPath = report.CurrentPath;
    }

    private void ShowCleaningProgress(CleaningProgress report)
    {
        if (!IsCleaning)
        {
            return;
        }

        CleaningFilesText = report.RemovedFileCount.ToString("N0", French);
        CleaningFreedText = ByteSize.Format(report.RemovedBytes);
        CurrentPath = report.CurrentPath;
    }

    private void SetSelection(Func<ScanItemRow, bool> predicate)
    {
        foreach (var row in Items)
        {
            row.IsSelected = predicate(row);
        }
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(CleanButtonText));
        CleanCommand.NotifyCanExecuteChanged();
    }

    private void SetState(CleanerState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsCleaning));
        OnPropertyChanged(nameof(IsCleaned));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(HasCleanableItems));
        OnPropertyChanged(nameof(CanChangeDisk));
        OnPropertyChanged(nameof(HasNothingFound));
        OnPropertyChanged(nameof(NothingFoundText));
        OnPropertyChanged(nameof(CleanableText));
        OnPropertyChanged(nameof(CleanableSummary));
        OnPropertyChanged(nameof(FreedText));
        OnPropertyChanged(nameof(CleanedSummary));
        OnPropertyChanged(nameof(HasLockedFiles));
        OnPropertyChanged(nameof(LockedText));
        OnPropertyChanged(nameof(HasKeptRecentFiles));
        OnPropertyChanged(nameof(KeptRecentText));
        OnPropertyChanged(nameof(DashboardValue));
        OnPropertyChanged(nameof(DashboardCaption));
        AnalyzeCommand.NotifyCanExecuteChanged();
        CleanCommand.NotifyCanExecuteChanged();
    }
}
