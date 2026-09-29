using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Scanning;

// Read-only: measures the folders named by the rules and reports what a cleanup would remove.
public sealed class KnownLocationScanner(
    IReadOnlyList<CleaningRule> rules,
    TimeProvider clock,
    ILogger<KnownLocationScanner> logger) : IScanner
{
    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public string Id => "known-locations";

    public string Name => "Emplacements connus";

    public Task<IReadOnlyList<ScanItem>> ScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Scan(progress, cancellationToken), cancellationToken);

    private IReadOnlyList<ScanItem> Scan(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var tracker = new ScanProgressTracker(progress, Name);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ScanItem>();

        foreach (var rule in rules)
        {
            foreach (var rawPath in rule.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var folder = Resolve(rawPath);
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

    private DirectoryInfo? Resolve(string rawPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(rawPath);
        if (expanded.Contains('%') || !Directory.Exists(expanded))
        {
            return null;
        }

        var folder = new DirectoryInfo(expanded);

        // A rule folder that is itself a link could point anywhere, for example to the user's documents.
        if (folder.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            logger.LogWarning("Skipping {Folder}: it is a link to another location", folder.FullName);
            return null;
        }

        return folder;
    }

    private ScanItem Measure(CleaningRule rule, DirectoryInfo folder, ScanProgressTracker tracker, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - TimeSpan.FromDays(rule.MinimumAgeDays);
        long eligibleBytes = 0;
        long eligibleFiles = 0;
        long skippedFiles = 0;
        DateTime? lastModified = null;

        try
        {
            foreach (var file in folder.EnumerateFiles("*", RecursiveOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                tracker.AddFile(file.FullName, file.Length);

                if (file.LastWriteTimeUtc > cutoff)
                {
                    skippedFiles++;
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
            SkippedFileCount: skippedFiles);
    }
}
