using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.FileSystem;

public sealed class StorageAnalyzer(ILogger<StorageAnalyzer> logger) : IStorageAnalyzer
{
    private const string ScannerName = "Stockage";

    // Junctions and symbolic links are never followed. "C:\Documents and Settings" points to C:\Users,
    // so following it would count every user profile twice, and a link can point outside the analyzed disk.
    private static readonly EnumerationOptions TopLevelOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public Task<IReadOnlyList<StorageUsage>> AnalyzeAsync(
        string rootPath,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => Analyze(rootPath, progress, cancellationToken), cancellationToken);

    private IReadOnlyList<StorageUsage> Analyze(string rootPath, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var tracker = new ScanProgressTracker(progress, ScannerName);
        var usages = new List<StorageUsage>();

        foreach (var entry in new DirectoryInfo(rootPath).EnumerateFileSystemInfos("*", TopLevelOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sizeBytes = entry is DirectoryInfo directory
                ? MeasureDirectory(directory, tracker, cancellationToken)
                : MeasureFile((FileInfo)entry, tracker);

            usages.Add(new StorageUsage(entry.Name, sizeBytes, entry.FullName, IsDirectory: entry is DirectoryInfo));
        }

        tracker.Report();
        return usages;
    }

    private long MeasureDirectory(DirectoryInfo directory, ScanProgressTracker tracker, CancellationToken cancellationToken)
    {
        long totalBytes = 0;

        try
        {
            foreach (var file in directory.EnumerateFiles("*", RecursiveOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalBytes += MeasureFile(file, tracker);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not fully measure {Directory}", directory.FullName);
        }

        return totalBytes;
    }

    private static long MeasureFile(FileInfo file, ScanProgressTracker tracker)
    {
        tracker.AddFile(file.FullName, file.Length);
        return file.Length;
    }
}
