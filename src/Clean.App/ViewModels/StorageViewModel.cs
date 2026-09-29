using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

public sealed class StorageViewModel : ObservableObject
{
    private const int ChartSliceCount = 8;

    // Sizes on disk and in the file table never match exactly (compression, hard links, locked files),
    // so the bar stays below 100 % until the analysis has really finished.
    private const double MaxRunningPercent = 99;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IDiskService _diskService;
    private readonly IStorageAnalyzer _storageAnalyzer;
    private readonly ILogger<StorageViewModel> _logger;

    private StorageViewState _state = StorageViewState.Idle;
    private int _selectedDiskIndex = -1;
    private string _scanTitle = string.Empty;
    private double _progressPercent;
    private string _filesScannedText = "0";
    private string _dataAnalyzedText = ByteSize.Format(0);
    private string _elapsedText = FormatElapsed(TimeSpan.Zero);
    private string _currentPath = string.Empty;
    private string? _statusMessage;
    private IReadOnlyList<StorageUsage> _buckets = [];
    private IReadOnlyList<StorageBucketRow> _bucketRows = [];
    private string _resultDiskName = string.Empty;

    public StorageViewModel(IDiskService diskService, IStorageAnalyzer storageAnalyzer, ILogger<StorageViewModel> logger)
    {
        _diskService = diskService;
        _storageAnalyzer = storageAnalyzer;
        _logger = logger;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => SelectedDisk is not null);
        CancelCommand = AnalyzeCommand.CreateCancelCommand();
    }

    public ObservableCollection<DiskItemViewModel> Disks { get; } = [];

    public IAsyncRelayCommand AnalyzeCommand { get; }

    public ICommand CancelCommand { get; }

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

    public bool IsIdle => _state == StorageViewState.Idle;

    public bool IsScanning => _state == StorageViewState.Scanning;

    public bool CanChangeDisk => !IsScanning;

    public bool HasResult => _state == StorageViewState.Completed;

    public string ScanTitle
    {
        get => _scanTitle;
        private set => SetProperty(ref _scanTitle, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

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

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
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

    public IReadOnlyList<StorageUsage> Buckets
    {
        get => _buckets;
        private set => SetProperty(ref _buckets, value);
    }

    public IReadOnlyList<StorageBucketRow> BucketRows
    {
        get => _bucketRows;
        private set => SetProperty(ref _bucketRows, value);
    }

    public string ResultSummary => $"{_resultDiskName} — {FilesScannedText} fichiers analysés en {ElapsedText}";

    private DiskItemViewModel? SelectedDisk =>
        SelectedDiskIndex >= 0 && SelectedDiskIndex < Disks.Count ? Disks[SelectedDiskIndex] : null;

    public async Task EnsureDisksLoadedAsync()
    {
        if (Disks.Count > 0)
        {
            return;
        }

        try
        {
            foreach (var disk in await _diskService.GetDisksAsync(CancellationToken.None))
            {
                Disks.Add(new DiskItemViewModel(disk));
            }

            SelectedDiskIndex = Disks.Count > 0 ? 0 : -1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Could not list the disks");
            StatusMessage = "Impossible de lire les disques de ce PC.";
        }
    }

    public async Task AnalyzeSystemDiskAsync()
    {
        await EnsureDisksLoadedAsync();
        if (AnalyzeCommand.IsRunning)
        {
            return;
        }

        var systemDisk = Disks.FirstOrDefault(disk => disk.IsSystemDrive);
        if (systemDisk is not null)
        {
            SelectedDiskIndex = Disks.IndexOf(systemDisk);
        }

        await AnalyzeCommand.ExecuteAsync(null);
    }

    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        if (SelectedDisk is not { } selected)
        {
            return;
        }

        var disk = selected.Disk;
        StartScan(selected.Name);
        var progress = new Progress<ScanProgress>(report => ShowProgress(report, disk));

        try
        {
            var usages = await _storageAnalyzer.AnalyzeAsync(disk.RootPath, progress, cancellationToken);
            ShowResult(usages, selected.Name);
        }
        catch (OperationCanceledException)
        {
            SetState(StorageViewState.Idle);
            StatusMessage = "Analyse annulée.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Storage analysis of {Disk} failed", disk.RootPath);
            SetState(StorageViewState.Idle);
            StatusMessage = $"L'analyse de {selected.Name} a échoué : le disque n'est pas accessible.";
        }
    }

    private void StartScan(string diskName)
    {
        StatusMessage = null;
        ScanTitle = $"Analyse de {diskName} en cours…";
        ProgressPercent = 0;
        FilesScannedText = "0";
        DataAnalyzedText = ByteSize.Format(0);
        ElapsedText = FormatElapsed(TimeSpan.Zero);
        CurrentPath = string.Empty;
        SetState(StorageViewState.Scanning);
    }

    private void ShowProgress(ScanProgress report, DiskInfo disk)
    {
        // Progress<T> delivers reports asynchronously, so one can still arrive after the scan has ended.
        if (!IsScanning)
        {
            return;
        }

        FilesScannedText = report.FilesScanned.ToString("N0", French);
        DataAnalyzedText = ByteSize.Format(report.BytesAnalyzed);
        ElapsedText = FormatElapsed(report.Elapsed);
        CurrentPath = report.CurrentPath;
        ProgressPercent = disk.UsedBytes > 0 ? Math.Min(MaxRunningPercent, report.BytesAnalyzed * 100.0 / disk.UsedBytes) : 0;
    }

    private void ShowResult(IReadOnlyList<StorageUsage> usages, string diskName)
    {
        _resultDiskName = diskName;
        Buckets = StorageBuckets.TopWithRemainder(usages, ChartSliceCount);
        BucketRows = Buckets.Select(bucket => new StorageBucketRow(bucket.Label, ByteSize.Format(bucket.SizeBytes))).ToList();
        ProgressPercent = 100;
        SetState(StorageViewState.Completed);
        OnPropertyChanged(nameof(ResultSummary));
    }

    private void SetState(StorageViewState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(CanChangeDisk));
        OnPropertyChanged(nameof(HasResult));
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds:00} s" : $"{elapsed.Seconds} s";
}
