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
    private readonly ILogger<CleanerViewModel> _logger;

    private StorageViewState _state = StorageViewState.Idle;
    private string _filesScannedText = "0";
    private string _dataAnalyzedText = ByteSize.Format(0);
    private string _currentPath = string.Empty;
    private string? _statusMessage;
    private ScanResult _result = ScanResult.Empty;
    private IReadOnlyList<ScanItemRow> _items = [];

    public CleanerViewModel(IScanManager scanManager, ILogger<CleanerViewModel> logger)
    {
        _scanManager = scanManager;
        _logger = logger;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync);
        CancelCommand = AnalyzeCommand.CreateCancelCommand();
    }

    public IAsyncRelayCommand AnalyzeCommand { get; }

    public ICommand CancelCommand { get; }

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
        $"{_result.CleanableFileCount.ToString("N0", French)} fichiers dans {_result.Items.Count(item => item.CanClean)} emplacements, analysés en {_result.Duration.TotalSeconds.ToString("0.#", French)} s";

    public string DashboardValue => HasResult ? CleanableText : "—";

    public string DashboardCaption => HasResult ? "Estimation, rien n'a été supprimé" : "Pas encore analysé";

    private async Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        StatusMessage = null;
        FilesScannedText = "0";
        DataAnalyzedText = ByteSize.Format(0);
        CurrentPath = string.Empty;
        SetState(StorageViewState.Scanning);

        var progress = new Progress<ScanProgress>(ShowProgress);
        try
        {
            _result = await _scanManager.RunAsync(progress, cancellationToken);
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
        OnPropertyChanged(nameof(CleanableText));
        OnPropertyChanged(nameof(CleanableSummary));
        OnPropertyChanged(nameof(DashboardValue));
        OnPropertyChanged(nameof(DashboardCaption));
    }
}
