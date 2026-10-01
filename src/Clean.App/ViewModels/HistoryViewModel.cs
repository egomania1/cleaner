using System.Globalization;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Clean.App.ViewModels;

public sealed class HistoryViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ICleaningArchive _archive;
    private readonly TimeProvider _clock;
    private readonly ILogger<HistoryViewModel> _logger;
    private readonly DispatcherQueue _dispatcher;

    private IReadOnlyList<CleaningSessionRow> _sessions = [];
    private bool _isBusy;
    private string? _statusMessage;

    public HistoryViewModel(ICleaningArchive archive, TimeProvider clock, ILogger<HistoryViewModel> logger)
    {
        _archive = archive;
        _clock = clock;
        _logger = logger;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // A cleaning finished on the Nettoyage page must show up here without reopening the page.
        _archive.Changed += () => _dispatcher.TryEnqueue(async () => await LoadAsync());
    }

    public Func<CleaningConfirmation, Task<bool>>? ConfirmAsync { get; set; }

    public IReadOnlyList<CleaningSessionRow> Sessions
    {
        get => _sessions;
        private set
        {
            if (SetProperty(ref _sessions, value))
            {
                OnPropertyChanged(nameof(HasSessions));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(RestorableText));
                OnPropertyChanged(nameof(RestorableCaption));
                OnPropertyChanged(nameof(FreedText));
                OnPropertyChanged(nameof(FreedCaption));
            }
        }
    }

    public bool HasSessions => Sessions.Count > 0;

    public bool IsEmpty => Sessions.Count == 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

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

    public string RestorableText => ByteSize.Format(Restorable.Sum(session => session.SizeBytes));

    public string RestorableCaption => $"Mis de côté dans {Restorable.Count()} nettoyage(s), encore restaurable";

    public string FreedText => ByteSize.Format(Sessions.Where(row => row.Session.Status == CleaningSessionStatus.Freed).Sum(row => row.Session.SizeBytes));

    public string FreedCaption => "Rendu au disque pour de bon";

    private IEnumerable<CleaningSession> Restorable => Sessions.Select(row => row.Session).Where(session => session.CanRestore);

    public async Task LoadAsync()
    {
        try
        {
            var now = _clock.GetUtcNow();
            Sessions = (await _archive.GetSessionsAsync(CancellationToken.None))
                .Select(session => new CleaningSessionRow(session, now, RestoreAsync, FreeAsync))
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Could not read the cleaning history");
            StatusMessage = "Impossible de lire l'historique des nettoyages.";
        }
    }

    private async Task RestoreAsync(CleaningSession session)
    {
        await RunAsync(async () =>
        {
            var result = await _archive.RestoreAsync(session.Id, CancellationToken.None);
            var parts = new List<string>
            {
                $"{result.RestoredFileCount.ToString("N0", French)} fichiers restaurés ({ByteSize.Format(result.RestoredBytes)}) à leur emplacement d'origine.",
            };

            if (result.ConflictFileCount > 0)
            {
                parts.Add($"{result.ConflictFileCount.ToString("N0", French)} avaient déjà été recréés par leur application : la version actuelle est gardée.");
            }

            if (result.FailedFileCount > 0)
            {
                parts.Add($"{result.FailedFileCount.ToString("N0", French)} n'ont pas pu revenir (dossier protégé ou fichier ouvert). Ils restent dans l'historique, réessaie plus tard.");
            }

            StatusMessage = string.Join(" ", parts);
        });
    }

    private async Task FreeAsync(CleaningSession session)
    {
        var confirmation = new CleaningConfirmation(
            "Libérer l'espace maintenant ?",
            $"{ByteSize.Format(session.SizeBytes)} ({session.FileCount.ToString("N0", French)} fichiers) vont être supprimés définitivement. Ce nettoyage ne pourra plus être restauré.",
            null,
            $"Libérer {ByteSize.Format(session.SizeBytes)}");

        if (ConfirmAsync is null || !await ConfirmAsync(confirmation))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var freed = await _archive.FreeAsync(session.Id, CancellationToken.None);
            StatusMessage = $"{ByteSize.Format(freed)} rendus au disque.";
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "History action failed");
            StatusMessage = "L'opération a échoué. Regarde l'historique pour voir ce qui a été fait.";
        }
        finally
        {
            IsBusy = false;
            await LoadAsync();
        }
    }
}
