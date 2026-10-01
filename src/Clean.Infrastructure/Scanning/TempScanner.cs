using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Scanning;

// Read-only: looks at every file of the temporary folders and only counts the ones nothing points to
// keeping; the others are reported with the reason they stay.
public sealed class TempScanner(
    IReadOnlyList<CleaningRule> rules,
    IReparsePointDetector reparsePointDetector,
    TimeProvider clock,
    ILogger<TempScanner> logger) : IScanner
{
    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public string Id => "temp";

    public string Name => "Fichiers temporaires";

    public Task<IReadOnlyList<ScanItem>> ScanAsync(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Scan(driveRoot, progress, cancellationToken), cancellationToken);

    private IReadOnlyList<ScanItem> Scan(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var tracker = new ScanProgressTracker(progress, Name);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ScanItem>();

        foreach (var rule in rules)
        {
            foreach (var rawPath in rule.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // %TEMP%, %TMP% and %LOCALAPPDATA%\Temp are usually the same folder.
                var folder = RuleFolders.Resolve(rawPath, driveRoot, reparsePointDetector, logger);
                if (folder is null || !visited.Add(Path.TrimEndingDirectorySeparator(folder.FullName)))
                {
                    continue;
                }

                var item = Measure(rule, folder, tracker, cancellationToken);
                if (item.FileCount > 0 || item.SkippedFileCount > 0)
                {
                    items.Add(item);
                }
            }
        }

        tracker.Report();
        return items;
    }

    private ScanItem Measure(CleaningRule rule, DirectoryInfo folder, ScanProgressTracker tracker, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(rule.MinimumAgeDays);
        var kept = new Dictionary<KeptFileReason, (long Files, long Bytes)>();
        long eligibleBytes = 0;
        long eligibleFiles = 0;
        DateTime? lastModified = null;

        try
        {
            foreach (var file in folder.EnumerateFiles("*", RecursiveOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                tracker.AddFile(file.FullName, file.Length);

                var reason = TempFilePolicy.FindReasonToKeep(file.Name, file.Attributes, file.LastWriteTimeUtc, cutoff)
                    ?? FileMoveProbe.FindReasonItCannotMove(file.FullName);
                if (reason is { } keptReason)
                {
                    var (files, bytes) = kept.GetValueOrDefault(keptReason);
                    kept[keptReason] = (files + 1, bytes + file.Length);
                    continue;
                }

                eligibleBytes += file.Length;
                eligibleFiles++;
                if (lastModified is null || file.LastWriteTimeUtc > lastModified)
                {
                    lastModified = file.LastWriteTimeUtc;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not fully measure {Folder}", folder.FullName);
        }

        var keptFiles = kept
            .Select(entry => new KeptFiles(entry.Key, entry.Value.Files, entry.Value.Bytes))
            .OrderBy(entry => entry.Reason)
            .ToList();

        return new ScanItem(
            Path: folder.FullName,
            Name: rule.Name,
            SizeBytes: eligibleBytes,
            Category: rule.Category,
            Risk: rule.Risk,
            Reason: rule.Description,
            RuleId: rule.Id,
            LastModified: lastModified is null ? null : new DateTimeOffset(lastModified.Value, TimeSpan.Zero),
            CanClean: eligibleBytes > 0 && rule.Risk != RiskLevel.Blocked,
            RequiresConfirmation: rule.Risk != RiskLevel.Safe,
            FileCount: eligibleFiles,
            SkippedFileCount: keptFiles.Sum(entry => entry.FileCount),
            KeptFiles: keptFiles);
    }
}
