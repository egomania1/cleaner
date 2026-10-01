using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Scanning;

// Read-only: measures the folders named by the rules on one drive and reports what a cleanup would remove.
public sealed class KnownLocationScanner(
    IReadOnlyList<CleaningRule> rules,
    IReparsePointDetector reparsePointDetector,
    TimeProvider clock,
    ILogger<KnownLocationScanner> logger,
    Func<string, bool>? isProcessRunning = null) : IScanner
{
    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private readonly Func<string, bool> _isRunning = isProcessRunning ?? IsRunning;

    public string Id => "known-locations";

    public string Name => "Emplacements connus";

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

        // A cache whose program is open is not a safe thing to clean: it is shown with a warning and not ticked by default.
        var isOpen = rule.ProcessName is { } process && _isRunning(process);
        var risk = isOpen && rule.Risk < RiskLevel.Caution ? RiskLevel.Caution : rule.Risk;

        return new ScanItem(
            Path: folder.FullName,
            Name: rule.Name,
            SizeBytes: eligibleBytes,
            Category: rule.Category,
            Risk: risk,
            Reason: isOpen ? $"Ce programme est ouvert : ferme-le pour que le nettoyage soit complet et propre. {rule.Description}" : rule.Description,
            RuleId: rule.Id,
            LastModified: lastModified is null ? null : new DateTimeOffset(lastModified.Value, TimeSpan.Zero),
            CanClean: eligibleBytes > 0 && rule.Risk != RiskLevel.Blocked,
            RequiresConfirmation: risk != RiskLevel.Safe,
            FileCount: eligibleFiles,
            SkippedFileCount: skippedFiles);
    }

    private static bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
