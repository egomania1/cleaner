using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

// Dry run: the scan only measures and explains. Nothing in this view model can delete a file.
public sealed class CleanerViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IScanManager _scanManager;
    private readonly IDiskService _diskService;
    private readonly ILogger<CleanerViewModel> _logger;

    private Task? _disksLoading;
    private int _selectedDiskIndex = -1;
    private string _resultDiskName = string.Empty;
    private bool _resultIsSystemDrive;
    private StorageViewState _state = StorageViewState.Idle;
    private string _filesScannedText = "0";
    private string _dataAnalyzedText = ByteSize.Format(0);
    private string _currentPath = string.Empty;
    private string? _statusMessage;
    private ScanResult _result = ScanResult.Empty;
    private IReadOnlyList<ScanItemRow> _items = [];

    public CleanerViewModel(IScanManager scanManager, IDiskService diskService, ILogger<CleanerViewModel> logger)
    {
        _scanManager = scanManager;
        _diskService = diskService;
        _logger = logger;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => SelectedDisk is not null);
        CancelCommand = AnalyzeCommand.CreateCancelCommand();
    }

    public IAsyncRelayCommand AnalyzeCommand { get; }

    public ICommand CancelCommand { get; }

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

    public bool CanChangeDisk => !IsScanning;

    public bool IsIdle => _state == StorageViewState.Idle;

    public bool IsScanning => _state == StorageViewState.Scanning;

    public bool HasResult => _state == StorageViewState.Completed;

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

    // The rules only know Windows, user profile and developer folders for now, and those live on the system drive.
    public bool HasNothingFound => HasResult && _result.Items.Count == 0;

    public string NothingFoundText => _resultIsSystemDrive
        ? $"Rien à nettoyer sur {_resultDiskName} pour l'instant."
        : $"Rien trouvé sur {_resultDiskName}. Les règles actuelles visent surtout des dossiers de Windows et de ton compte, qui sont sur le disque système.";

    public string DashboardValue => HasResult ? CleanableText : "—";

    public string DashboardCaption => HasResult ? "Estimation, rien n'a été supprimé" : "Pas encore analysé";

    private DiskItemViewModel? SelectedDisk =>
        SelectedDiskIndex >= 0 && SelectedDiskIndex < Disks.Count ? Disks[SelectedDiskIndex] : null;

    public Task EnsureDisksLoadedAsync() => _disksLoading ??= LoadDisksAsync();

    private async Task LoadDisksAsync()
    {
        try
        {
            foreach (var disk in await _diskService.GetDisksAsync(CancellationToken.None))
            {
                Disks.Add(new DiskItemViewModel(disk));
            }

            var systemDisk = Disks.FirstOrDefault(disk => disk.IsSystemDrive);
            SelectedDiskIndex = systemDisk is not null ? Disks.IndexOf(systemDisk) : Disks.Count > 0 ? 0 : -1;
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
        SetState(StorageViewState.Scanning);

        var progress = new Progress<ScanProgress>(ShowProgress);
        try
        {
            _result = await _scanManager.RunAsync(selected.Disk.RootPath, progress, cancellationToken);
            _resultDiskName = selected.Name;
            _resultIsSystemDrive = selected.IsSystemDrive;
            Items = _result.Items.OrderByDescending(item => item.SizeBytes).Select(item => new ScanItemRow(item)).ToList();

            if (_result.Errors.Count > 0)
            {
                StatusMessage = $"{_result.Errors.Count} analyse(s) n'ont pas pu aller au bout : {string.Join(" ; ", _result.Errors.Select(error => error.Message))}";
            }

            SetState(StorageViewState.Completed);
        }
        catch (OperationCanceledException)
        {
            SetState(StorageViewState.Idle);
            StatusMessage = "Analyse annulée.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Cleanup analysis failed");
            SetState(StorageViewState.Idle);
            StatusMessage = "L'analyse a échoué : un emplacement n'était pas accessible.";
        }
    }

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

    private void SetState(StorageViewState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(CanChangeDisk));
        OnPropertyChanged(nameof(HasNothingFound));
        OnPropertyChanged(nameof(NothingFoundText));
        OnPropertyChanged(nameof(CleanableText));
        OnPropertyChanged(nameof(CleanableSummary));
        OnPropertyChanged(nameof(DashboardValue));
        OnPropertyChanged(nameof(DashboardCaption));
    }
}
