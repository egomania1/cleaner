using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Cleaning;

// Removes only what the scan promised: files older than the rule's minimum age, inside the rule folder,
// never through a link. Files are moved to the archive rather than deleted, so the cleaning can be undone.
public sealed class FileCleaner(
    ISafetyEngine safetyEngine,
    IRuleEngine ruleEngine,
    ICleaningArchive archive,
    TimeProvider clock,
    ILogger<FileCleaner> logger) : ICleaner
{
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public async Task<CleaningResult> CleanAsync(
        IReadOnlyList<CleaningDecision> decisions,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sessionId = archive.CreateSessionId();
        var run = new Run(progress, Stopwatch.StartNew());
        var locations = await Task.Run(() => Clean(sessionId, decisions, run, cancellationToken), CancellationToken.None);

        CleaningSession? session = null;
        if (run.RemovedFiles > 0)
        {
            var now = clock.GetUtcNow();
            session = new CleaningSession(
                sessionId,
                now,
                now + CleaningSession.RetentionPeriod,
                Path.GetPathRoot(decisions[0].Item.Path)?.TrimEnd('\\') ?? string.Empty,
                run.RemovedBytes,
                run.RemovedFiles,
                locations.Where(location => location.FileCount > 0).ToList(),
                CleaningSessionStatus.Restorable,
                null);

            // Recorded even after a cancellation: whatever was moved must stay restorable.
            await archive.RecordAsync(session, CancellationToken.None);
        }

        return new CleaningResult(
            run.RemovedBytes,
            run.RemovedFiles,
            run.LockedFiles,
            run.KeptRecentFiles,
            locations.Count,
            cancellationToken.IsCancellationRequested,
            run.Stopwatch.Elapsed,
            session);
    }

    private List<CleaningSessionLocation> Clean(string sessionId, IReadOnlyList<CleaningDecision> decisions, Run run, CancellationToken cancellationToken)
    {
        var locations = new List<CleaningSessionLocation>();

        foreach (var decision in decisions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // The decision is checked again here: a view model bug must never be enough to remove something.
            if (!decision.IsAllowed || !safetyEngine.Evaluate(decision.Item).IsAllowed)
            {
                logger.LogWarning("Refusing to clean {Path}", decision.Item.Path);
                continue;
            }

            var rule = ruleEngine.Rules.FirstOrDefault(candidate => candidate.Id == decision.Item.RuleId);
            var folder = new DirectoryInfo(decision.Item.Path);
            if (rule is null || !folder.Exists || folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var index = locations.Count;
            var bytesBefore = run.RemovedBytes;
            var filesBefore = run.RemovedFiles;
            var cutoff = clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(rule.MinimumAgeDays);

            CleanFolder(folder, archive.GetLocationFolder(sessionId, index, folder.FullName), cutoff, run, cancellationToken);
            locations.Add(new CleaningSessionLocation(
                index,
                rule.Id,
                rule.Name,
                folder.FullName,
                run.RemovedBytes - bytesBefore,
                run.RemovedFiles - filesBefore));
        }

        run.Report(string.Empty);
        return locations;
    }

    private void CleanFolder(DirectoryInfo folder, string archiveFolder, DateTime cutoff, Run run, CancellationToken cancellationToken)
    {
        List<FileInfo> files;
        try
        {
            // Listed first so that moving files does not disturb the enumeration.
            files = folder.EnumerateFiles("*", RecursiveOptions).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not list {Folder}", folder.FullName);
            return;
        }

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ArchiveFile(file, Path.Combine(archiveFolder, Path.GetRelativePath(folder.FullName, file.FullName)), cutoff, run);
        }

        DeleteEmptySubfolders(folder, cutoff);
    }

    private static void ArchiveFile(FileInfo file, string destination, DateTime cutoff, Run run)
    {
        try
        {
            file.Refresh();
            if (!file.Exists)
            {
                return;
            }

            // Checked again at cleaning time: a file written since the scan is in use again.
            if (file.LastWriteTimeUtc > cutoff)
            {
                run.KeptRecentFiles++;
                return;
            }

            var size = file.Length;
            if (file.IsReadOnly)
            {
                file.IsReadOnly = false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            file.MoveTo(destination);
            run.RemovedFiles++;
            run.RemovedBytes += size;
            run.MaybeReport(file.FullName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Usually a file held open by a running application, or a Windows file that needs administrator rights.
            run.LockedFiles++;
        }
    }

    // Moving a file out makes its folder look recent, so the creation date tells apart a folder an app just made.
    private void DeleteEmptySubfolders(DirectoryInfo folder, DateTime cutoff)
    {
        List<DirectoryInfo> subfolders;
        try
        {
            subfolders = folder.EnumerateDirectories("*", RecursiveOptions)
                .OrderByDescending(subfolder => subfolder.FullName.Length)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not list the subfolders of {Folder}", folder.FullName);
            return;
        }

        foreach (var subfolder in subfolders)
        {
            try
            {
                if (subfolder.CreationTimeUtc <= cutoff && !subfolder.EnumerateFileSystemInfos().Any())
                {
                    subfolder.Delete(recursive: false);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(exception, "Kept the folder {Folder}", subfolder.FullName);
            }
        }
    }

    private sealed class Run(IProgress<CleaningProgress>? progress, Stopwatch stopwatch)
    {
        private TimeSpan _lastReport = TimeSpan.Zero;

        public Stopwatch Stopwatch => stopwatch;

        public long RemovedBytes { get; set; }

        public long RemovedFiles { get; set; }

        public long LockedFiles { get; set; }

        public long KeptRecentFiles { get; set; }

        public void MaybeReport(string path)
        {
            if (stopwatch.Elapsed - _lastReport >= ReportInterval)
            {
                Report(path);
            }
        }

        public void Report(string path)
        {
            _lastReport = stopwatch.Elapsed;
            progress?.Report(new CleaningProgress(path, RemovedFiles, RemovedBytes));
        }
    }
}
