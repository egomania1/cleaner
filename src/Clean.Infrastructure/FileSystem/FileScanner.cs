using System.Diagnostics;
using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.FileSystem;

// Walks the user's part of a drive. Folders are walked by hand rather than with a recursive enumeration
// so that whole excluded trees (Windows, AppData…) are skipped instead of listed and filtered.
public sealed class FileScanner(UserFileScope scope, ILogger<FileScanner> logger) : IFileScanner
{
    private const string ScannerName = "Fichiers";

    // Cloud placeholders (OneDrive "files on demand"): their content is not on the disk yet.
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    private const FileAttributes CloudOnly = FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess;

    private static readonly EnumerationOptions Options = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };

    public Task<FileScanResult> ScanAsync(string driveRoot, long minimumSizeBytes, IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Scan(driveRoot, minimumSizeBytes, progress, cancellationToken), cancellationToken);

    private FileScanResult Scan(string driveRoot, long minimumSizeBytes, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var tracker = new ScanProgressTracker(progress, ScannerName);
        var found = new List<FoundFile>();
        long cloudOnly = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(driveRoot));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            var isRoot = folder.Parent is null;

            try
            {
                foreach (var entry in folder.EnumerateFileSystemInfos("*", Options))
                {
                    // Links are never followed: they can point anywhere, including back up the tree.
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint) && (entry.Attributes & CloudOnly) == 0)
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo directory)
                    {
                        if (!scope.IsExcludedFolder(directory.FullName))
                        {
                            pending.Push(directory);
                        }

                        continue;
                    }

                    if ((entry.Attributes & CloudOnly) != 0)
                    {
                        cloudOnly++;
                        continue;
                    }

                    // Files at a drive root and system-flagged files are Windows' own (pagefile.sys, desktop.ini…).
                    if (isRoot || entry.Attributes.HasFlag(FileAttributes.System))
                    {
                        continue;
                    }

                    var file = (FileInfo)entry;
                    tracker.AddFile(file.FullName, file.Length);
                    if (file.Length >= minimumSizeBytes)
                    {
                        found.Add(new FoundFile(file.FullName, file.Length, file.LastWriteTimeUtc));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(exception, "Could not list {Folder}", folder.FullName);
            }
        }

        tracker.Report();
        return new FileScanResult(found, tracker.FilesScanned, cloudOnly, stopwatch.Elapsed);
    }
}
