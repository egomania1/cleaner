using Clean.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.FileSystem;

public sealed class FolderSizer(ILogger<FolderSizer> logger) : IFolderSizer
{
    // Links are skipped: a game library linked to another drive must not be counted twice.
    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    public Task<long> MeasureAsync(string folder, IReadOnlyCollection<string> excludedFolders, CancellationToken cancellationToken) =>
        Task.Run(() => Measure(folder, excludedFolders, cancellationToken), cancellationToken);

    private long Measure(string folder, IReadOnlyCollection<string> excludedFolders, CancellationToken cancellationToken)
    {
        var excluded = excludedFolders.Select(path => Path.TrimEndingDirectorySeparator(path) + '\\').ToList();
        long total = 0;
        try
        {
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", RecursiveOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!excluded.Exists(prefix => file.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    total += file.Length;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Could not fully measure {Folder}", folder);
        }

        return total;
    }
}
