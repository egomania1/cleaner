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

// What the Doublons and Gros fichiers pages share: pick a drive, analyse it, tick files, remove them
// into the archive (restorable from the history) after a confirmation.
public abstract class FileToolViewModel : ObservableObject
{
    protected static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IDiskService _diskService;
    private readonly IFileRemover _remover;
    private readonly IFileExplorer _explorer;

    private Task? _disksLoading;
    private int _selectedDiskIndex = -1;
    private int _selectedSizeIndex;
    private CleanerState _state = CleanerState.Idle;
    private string _phaseText = string.Empty;
    private string _filesScannedText = "0";
    private string _secondaryProgressText = string.Empty;
    private string _currentPath = string.Empty;
    private double _progressRatio;
    private bool _isProgressIndeterminate = true;
    private string? _statusMessage;
    private CleaningResult? _removal;

    protected FileToolViewModel(
        IDiskService diskService,
        IFileScanner scanner,
        IFileRemover remover,
        IFileExplorer explorer,
        NavigationService navigation,
        ILogger logger)
    {
        _diskService = diskService;
        Scanner = scanner;
        _remover = remover;
        _explorer = explorer;
        Logger = logger;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => SelectedDisk is not null && !IsRemoving);
        CancelCommand = AnalyzeCommand.CreateCancelCommand();
        RemoveCommand = new AsyncRelayCommand(RemoveAsync, () => IsReady && SelectedBytes > 0);
        CancelRemoveCommand = RemoveCommand.CreateCancelCommand();
        ShowHistoryCommand = new RelayCommand(() => navigation.NavigateTo("history"));
    }

    public IAsyncRelayCommand AnalyzeCommand { get; }

    public ICommand CancelCommand { get; }

    public IAsyncRelayCommand RemoveCommand { get; }

    public ICommand CancelRemoveCommand { get; }

    public ICommand ShowHistoryCommand { get; }

    public Func<CleaningConfirmation, Task<bool>>? ConfirmAsync { get; set; }

    public ObservableCollection<DiskItemViewModel> Disks { get; } = [];

    public abstract IReadOnlyList<string> SizeOptions { get; }

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

    public int SelectedSizeIndex
    {
        get => _selectedSizeIndex;
        set => SetProperty(ref _selectedSizeIndex, value);
    }

    public bool CanChangeSettings => !IsScanning && !IsRemoving;

    public bool IsIdle => _state == CleanerState.Idle;

    public bool IsScanning => _state == CleanerState.Scanning;

    public bool IsReady => _state == CleanerState.Ready;

    public bool IsRemoving => _state == CleanerState.Cleaning;

    public bool IsRemoved => _state == CleanerState.Cleaned;

    public string PhaseText
    {
        get => _phaseText;
        protected set => SetProperty(ref _phaseText, value);
    }

    public string FilesScannedText
    {
        get => _filesScannedText;
        protected set => SetProperty(ref _filesScannedText, value);
    }

    public string SecondaryProgressText
    {
        get => _secondaryProgressText;
        protected set => SetProperty(ref _secondaryProgressText, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        protected set => SetProperty(ref _currentPath, value);
    }

    public double ProgressRatio
    {
        get => _progressRatio;
        protected set => SetProperty(ref _progressRatio, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        protected set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        protected set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => StatusMessage is not null;

    public abstract long SelectedBytes { get; }

    public abstract int SelectedCount { get; }

    public string SelectionText => SelectedCount == 0
        ? "Rien de sélectionné."
        : $"{ByteSize.Format(SelectedBytes)} sélectionnés — {SelectedCount.ToString("N0", French)} fichier{(SelectedCount > 1 ? "s" : string.Empty)}";

    public string RemoveButtonText => $"Retirer {ByteSize.Format(SelectedBytes)}";

    public string SelectedBytesText => ByteSize.Format(SelectedBytes);

    public string RemovedText => ByteSize.Format(_removal?.RemovedBytes ?? 0);

    public string RemovedSummary => _removal is { } result
        ? $"{result.RemovedFileCount.ToString("N0", French)} fichier{(result.RemovedFileCount > 1 ? "s" : string.Empty)} retiré{(result.RemovedFileCount > 1 ? "s" : string.Empty)}{(result.WasCancelled ? ", nettoyage arrêté en cours de route" : string.Empty)}. Ils restent restaurables {CleaningSession.RetentionPeriod.Days} jours depuis l'Historique."
        : string.Empty;

    public bool HasSkippedRemovals => _removal is { } result && result.LockedFileCount + result.KeptRecentFileCount > 0;

    public string SkippedRemovalsText => _removal is { } result
        ? $"{(result.LockedFileCount + result.KeptRecentFileCount).ToString("N0", French)} fichier(s) gardé(s) : ouverts dans une application, modifiés depuis l'analyse, ou leur copie à garder avait disparu."
        : string.Empty;

    protected IFileScanner Scanner { get; }

    protected ILogger Logger { get; }

    protected DiskItemViewModel? SelectedDisk =>
        SelectedDiskIndex >= 0 && SelectedDiskIndex < Disks.Count ? Disks[SelectedDiskIndex] : null;

    protected abstract string RuleId { get; }

    protected abstract string Label { get; }

    public Task EnsureDisksLoadedAsync() => _disksLoading ??= LoadDisksAsync();

    public void Reveal(string path) => _explorer.Reveal(path);

    protected abstract Task AnalyzeCoreAsync(DiskItemViewModel disk, CancellationToken cancellationToken);

    protected abstract IReadOnlyList<RemovalRequest> SelectedRequests();

    protected abstract string? SelectionWarning();

    protected void ShowScanProgress(ScanProgress report)
    {
        FilesScannedText = report.FilesScanned.ToString("N0", French);
        CurrentPath = report.CurrentPath;
    }

    protected void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedBytes));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectionText));
        OnPropertyChanged(nameof(RemoveButtonText));
        OnPropertyChanged(nameof(SelectedBytesText));
        RemoveCommand.NotifyCanExecuteChanged();
    }

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
            Logger.LogError(exception, "Could not list the disks");
            StatusMessage = "Impossible de lister les disques.";
        }
    }

    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        if (SelectedDisk is not { } disk)
        {
            return;
        }

        StatusMessage = null;
        FilesScannedText = "0";
        SecondaryProgressText = string.Empty;
        CurrentPath = string.Empty;
        ProgressRatio = 0;
        IsProgressIndeterminate = true;
        SetState(CleanerState.Scanning);

        try
        {
            await AnalyzeCoreAsync(disk, cancellationToken);
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
            Logger.LogError(exception, "Analysis failed");
            SetState(CleanerState.Idle);
            StatusMessage = "L'analyse a échoué : un dossier n'était pas accessible.";
        }
    }

    private async Task RemoveAsync(CancellationToken cancellationToken)
    {
        var requests = SelectedRequests();
        if (requests.Count == 0)
        {
            return;
        }

        var bytes = requests.Sum(request => request.SizeBytes);
        var confirmation = new CleaningConfirmation(
            "Retirer ces fichiers ?",
            $"{ByteSize.Format(bytes)} ({requests.Count.ToString("N0", French)} fichier{(requests.Count > 1 ? "s" : string.Empty)}) vont être retirés de leur dossier. " +
            $"Ils restent restaurables {CleaningSession.RetentionPeriod.Days} jours depuis l'Historique, puis l'espace est libéré pour de bon.",
            SelectionWarning(),
            $"Retirer {ByteSize.Format(bytes)}");

        if (ConfirmAsync is null || !await ConfirmAsync(confirmation))
        {
            return;
        }

        StatusMessage = null;
        FilesScannedText = "0";
        SecondaryProgressText = ByteSize.Format(0);
        CurrentPath = string.Empty;
        SetState(CleanerState.Cleaning);

        try
        {
            var progress = new Progress<CleaningProgress>(report =>
            {
                FilesScannedText = report.RemovedFileCount.ToString("N0", French);
                SecondaryProgressText = ByteSize.Format(report.RemovedBytes);
                CurrentPath = report.CurrentPath;
            });
            _removal = await _remover.RemoveAsync(requests, RuleId, Label, progress, cancellationToken);
            SetState(CleanerState.Cleaned);

            _disksLoading = LoadDisksAsync();
            await _disksLoading;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Logger.LogError(exception, "Removal failed");
            SetState(CleanerState.Ready);
            StatusMessage = "Le retrait a échoué. Relance l'analyse pour voir ce qui reste.";
        }
    }

    protected virtual void OnStateChanged()
    {
    }

    private void SetState(CleanerState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsRemoving));
        OnPropertyChanged(nameof(IsRemoved));
        OnPropertyChanged(nameof(CanChangeSettings));
        OnPropertyChanged(nameof(RemovedText));
        OnPropertyChanged(nameof(RemovedSummary));
        OnPropertyChanged(nameof(HasSkippedRemovals));
        OnPropertyChanged(nameof(SkippedRemovalsText));
        AnalyzeCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        OnStateChanged();
    }
}
