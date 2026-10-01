using System.Diagnostics;
using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Cleaning;

// Removes files the user picked by hand (duplicates, large files). Like the rule-based cleaner, it moves
// them to the archive, so every removal shows up in the history and can be undone.
public sealed class FileRemover(
    UserFileScope scope,
    IInstalledProgramCatalog programs,
    ICleaningArchive archive,
    TimeProvider clock,
    ILogger<FileRemover> logger) : IFileRemover
{
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

    public async Task<CleaningResult> RemoveAsync(
        IReadOnlyList<RemovalRequest> requests,
        string ruleId,
        string label,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var sessionId = archive.CreateSessionId();
        var outcome = new Outcome();

        CleaningSession? session = null;
        try
        {
            await Task.Run(() => Remove(sessionId, requests, ruleId, label, progress, stopwatch, outcome, cancellationToken), CancellationToken.None);
        }
        finally
        {
            // Recorded even after an unexpected error: whatever was moved must stay restorable.
            if (outcome.RemovedFiles > 0)
            {
                var now = clock.GetUtcNow();
                session = new CleaningSession(
                    sessionId,
                    now,
                    now + CleaningSession.RetentionPeriod,
                    Path.GetPathRoot(requests[0].Path)?.TrimEnd('\\') ?? string.Empty,
                    outcome.RemovedBytes,
                    outcome.RemovedFiles,
                    outcome.Locations.Where(location => location.FileCount > 0).ToList(),
                    CleaningSessionStatus.Restorable,
                    null);
                await archive.RecordAsync(session, CancellationToken.None);
            }
        }

        return new CleaningResult(
            outcome.RemovedBytes,
            outcome.RemovedFiles,
            outcome.LockedFiles,
            outcome.ChangedFiles,
            outcome.Locations.Count(location => location.FileCount > 0),
            cancellationToken.IsCancellationRequested,
            stopwatch.Elapsed,
            session);
    }

    private void Remove(
        string sessionId,
        IReadOnlyList<RemovalRequest> requests,
        string ruleId,
        string label,
        IProgress<CleaningProgress>? progress,
        Stopwatch stopwatch,
        Outcome outcome,
        CancellationToken cancellationToken)
    {
        var requested = requests.Select(request => request.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastReport = TimeSpan.Zero;

        foreach (var request in requests)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var file = new FileInfo(request.Path);
            if (Refusal(request, file, requested) is { } reason)
            {
                logger.LogWarning("Refusing to remove {File}: {Reason}", request.Path, reason);
                outcome.ChangedFiles++;
                continue;
            }

            var folder = file.DirectoryName!;
            if (!folders.TryGetValue(folder, out var index))
            {
                index = folders.Count;
                folders[folder] = index;
                outcome.Locations.Add(new CleaningSessionLocation(index, ruleId, label, folder, 0, 0));
            }

            try
            {
                var destination = Path.Combine(archive.GetLocationFolder(sessionId, index, folder), file.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (file.IsReadOnly)
                {
                    file.IsReadOnly = false;
                }

                file.MoveTo(destination);
                outcome.RemovedFiles++;
                outcome.RemovedBytes += request.SizeBytes;
                var location = outcome.Locations[index];
                outcome.Locations[index] = location with { SizeBytes = location.SizeBytes + request.SizeBytes, FileCount = location.FileCount + 1 };

                if (stopwatch.Elapsed - lastReport >= ReportInterval)
                {
                    lastReport = stopwatch.Elapsed;
                    progress?.Report(new CleaningProgress(request.Path, outcome.RemovedFiles, outcome.RemovedBytes));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Usually a file open in another application.
                logger.LogDebug(exception, "Could not remove {File}", request.Path);
                outcome.LockedFiles++;
            }
        }

        progress?.Report(new CleaningProgress(string.Empty, outcome.RemovedFiles, outcome.RemovedBytes));
    }

    // Everything the scan saw is checked again: the file must still be the one the user looked at.
    private string? Refusal(RemovalRequest request, FileInfo file, HashSet<string> requested)
    {
        if (!file.Exists)
        {
            return "the file is gone";
        }

        if (!scope.Contains(file.FullName))
        {
            return "outside the user's files";
        }

        if (file.Attributes.HasFlag(FileAttributes.ReparsePoint) || file.Attributes.HasFlag(FileAttributes.System))
        {
            return "a link or a system file";
        }

        if (ProgramMatcher.ProgramsAt(programs.All, file.FullName).Count > 0)
        {
            return "part of an installed application";
        }

        if (file.Length != request.SizeBytes || file.LastWriteTimeUtc != request.LastWriteUtc)
        {
            return "changed since the scan";
        }

        if (request.KeptCopyPath is { } kept)
        {
            var copy = new FileInfo(kept);
            if (requested.Contains(kept) || string.Equals(kept, request.Path, StringComparison.OrdinalIgnoreCase)
                || !copy.Exists || copy.Length != request.SizeBytes)
            {
                return "the copy that should stay is missing";
            }
        }

        return null;
    }

    private sealed class Outcome
    {
        public List<CleaningSessionLocation> Locations { get; } = [];

        public long RemovedBytes { get; set; }

        public long RemovedFiles { get; set; }

        public long LockedFiles { get; set; }

        public long ChangedFiles { get; set; }
    }
}
