using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Uninstall;

// Moves the chosen leftovers into the archive, so they show up in the history and can be restored for a week.
// The finder runs again first: only folders it still considers leftovers right now are touched.
public sealed class LeftoverRemover(
    ILeftoverFinder finder,
    ICleaningArchive archive,
    TimeProvider clock,
    ILogger<LeftoverRemover> logger) : ILeftoverRemover
{
    private const string RuleId = "UNINSTALL_LEFTOVERS";
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

    public async Task<CleaningResult> RemoveAsync(
        ProgramInfo program,
        IReadOnlyList<string> folders,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var search = await finder.FindAsync(program, cancellationToken);
        var allowed = search.Folders
            .Where(folder => folders.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
            .Select(folder => folder.Path)
            .ToList();

        var outcome = new Outcome();
        var sessionId = archive.CreateSessionId();
        await Task.Run(() => Move(program, allowed, sessionId, progress, stopwatch, outcome, cancellationToken), CancellationToken.None);

        CleaningSession? session = null;
        if (outcome.RemovedFiles > 0)
        {
            var now = clock.GetUtcNow();
            session = new CleaningSession(
                sessionId,
                now,
                now + CleaningSession.RetentionPeriod,
                Path.GetPathRoot(allowed[0])?.TrimEnd('\\') ?? string.Empty,
                outcome.RemovedBytes,
                outcome.RemovedFiles,
                outcome.Locations.Where(location => location.FileCount > 0).ToList(),
                CleaningSessionStatus.Restorable,
                null);
            await archive.RecordAsync(session, CancellationToken.None);
        }

        return new CleaningResult(
            outcome.RemovedBytes,
            outcome.RemovedFiles,
            outcome.LockedFiles,
            0,
            allowed.Count,
            cancellationToken.IsCancellationRequested,
            stopwatch.Elapsed,
            session);
    }

    private void Move(
        ProgramInfo program,
        IReadOnlyList<string> roots,
        string sessionId,
        IProgress<CleaningProgress>? progress,
        Stopwatch stopwatch,
        Outcome outcome,
        CancellationToken cancellationToken)
    {
        var lastReport = TimeSpan.Zero;
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var file in LeftoverFinder.EnumerateFiles(root, CancellationToken.None))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var folder = file.DirectoryName!;
                if (!indexes.TryGetValue(folder, out var index))
                {
                    index = outcome.Locations.Count;
                    indexes[folder] = index;
                    outcome.Locations.Add(new CleaningSessionLocation(index, RuleId, "Restes de " + program.Name, folder, 0, 0));
                }

                try
                {
                    var size = file.Length;
                    var destination = Path.Combine(archive.GetLocationFolder(sessionId, index, folder), file.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if (file.IsReadOnly)
                    {
                        file.IsReadOnly = false;
                    }

                    file.MoveTo(destination);
                    outcome.RemovedFiles++;
                    outcome.RemovedBytes += size;
                    var location = outcome.Locations[index];
                    outcome.Locations[index] = location with { SizeBytes = location.SizeBytes + size, FileCount = location.FileCount + 1 };

                    if (stopwatch.Elapsed - lastReport >= ReportInterval)
                    {
                        lastReport = stopwatch.Elapsed;
                        progress?.Report(new CleaningProgress(file.FullName, outcome.RemovedFiles, outcome.RemovedBytes));
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A file in use, or a folder that needs administrator rights (Program Files).
                    logger.LogDebug(exception, "Could not move the leftover {File}", file.FullName);
                    outcome.LockedFiles++;
                }
            }

            DeleteEmptyFolders(root);
        }

        progress?.Report(new CleaningProgress(string.Empty, outcome.RemovedFiles, outcome.RemovedBytes));
    }

    // Only folders with nothing left in them: a folder holding a file that could not be moved stays as it is.
    private static void DeleteEmptyFolders(string root)
    {
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
            {
                TryDeleteIfEmpty(folder);
            }

            TryDeleteIfEmpty(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort: an empty folder left behind is harmless.
        }
    }

    private static void TryDeleteIfEmpty(string folder)
    {
        try
        {
            if (!new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Same: best effort.
        }
    }

    private sealed class Outcome
    {
        public List<CleaningSessionLocation> Locations { get; } = [];

        public long RemovedBytes { get; set; }

        public long RemovedFiles { get; set; }

        public long LockedFiles { get; set; }
    }
}
