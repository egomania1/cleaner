using System.Diagnostics;
using Clean.Core.Developer;
using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.FileSystem;

// Finds the folders a build tool recreates by itself (node_modules, bin/obj, target…). A folder only counts
// when the project file that explains it sits next to it, and nothing inside it is ever followed through a link.
// Hidden folders (.git, .vscode, .cargo, .npm…) are never entered: what sits in them is managed by a tool
// (a VS Code extension ships its own node_modules, which is not recreated by npm install).
public sealed class DeveloperScanner(UserFileScope scope, ILogger<DeveloperScanner> logger) : IDeveloperScanner
{
    private const string ScannerName = "Développeur";

    private static readonly EnumerationOptions Options = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };

    public Task<DeveloperScanResult> ScanAsync(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Scan(driveRoot, progress, cancellationToken), cancellationToken);

    private DeveloperScanResult Scan(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var tracker = new ScanProgressTracker(progress, ScannerName);
        var found = new List<DevFolder>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(driveRoot));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();

            try
            {
                var entries = folder.EnumerateFileSystemInfos("*", Options).ToList();
                var fileNames = entries.OfType<FileInfo>().Select(file => file.Name).ToList();

                foreach (var directory in entries.OfType<DirectoryInfo>())
                {
                    if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint) || scope.IsExcludedFolder(directory.FullName))
                    {
                        continue;
                    }

                    if (DevFolderMatcher.Match(directory.Name, fileNames) is { } kind)
                    {
                        if (Measure(directory, kind, tracker, cancellationToken) is { } devFolder)
                        {
                            found.Add(devFolder);
                        }
                    }
                    else if (!directory.Name.StartsWith('.'))
                    {
                        pending.Push(directory);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(exception, "Could not list {Folder}", folder.FullName);
            }
        }

        tracker.Report();
        return new DeveloperScanResult(found, tracker.FilesScanned, stopwatch.Elapsed);
    }

    private DevFolder? Measure(DirectoryInfo root, DevFolderKind kind, ScanProgressTracker tracker, CancellationToken cancellationToken)
    {
        var files = new List<FoundFile>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();

            try
            {
                foreach (var entry in folder.EnumerateFileSystemInfos("*", Options))
                {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo directory)
                    {
                        pending.Push(directory);
                        continue;
                    }

                    var file = (FileInfo)entry;
                    tracker.AddFile(file.FullName, file.Length);
                    files.Add(new FoundFile(file.FullName, file.Length, file.LastWriteTimeUtc));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(exception, "Could not list {Folder}", folder.FullName);
            }
        }

        if (files.Count == 0)
        {
            return null;
        }

        return new DevFolder(root.FullName, kind, files.Sum(file => file.SizeBytes), files.Max(file => file.LastWriteUtc), files);
    }
}
