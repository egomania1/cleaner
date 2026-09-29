using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.FileSystem;

public sealed class EntryInspector(IInstalledProgramCatalog programCatalog, ILogger<EntryInspector> logger) : IEntryInspector
{
    // Junctions and symbolic links are never followed, for the same reasons as in StorageAnalyzer.
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

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".dll", ".sys" };

    public Task<EntryDetails> InspectAsync(string path, bool isDirectory, CancellationToken cancellationToken) =>
        Task.Run(() => isDirectory ? InspectFolder(path, cancellationToken) : InspectFile(path), cancellationToken);

    private EntryDetails InspectFile(string path)
    {
        var file = new FileInfo(path);
        var program = ExecutableExtensions.Contains(file.Extension) ? ReadProgram(file.FullName) : null;

        return new EntryDetails(
            IsDirectory: false,
            Created: file.CreationTime,
            LastModified: file.LastWriteTime,
            FileCount: 1,
            FolderCount: 0,
            Composition: [new ExtensionUsage(file.Extension, file.Length, 1)],
            Programs: program is null ? [] : [program],
            Children: []);
    }

    private EntryDetails InspectFolder(string path, CancellationToken cancellationToken)
    {
        var folder = new DirectoryInfo(path);
        var totals = new FolderTotals();
        var children = new List<StorageUsage>();

        foreach (var entry in folder.EnumerateFileSystemInfos("*", TopLevelOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry is DirectoryInfo directory)
            {
                totals.AddFolder();
                children.Add(new StorageUsage(directory.Name, MeasureFolder(directory, totals, cancellationToken), directory.FullName, IsDirectory: true));
            }
            else if (entry is FileInfo file)
            {
                totals.AddFile(file);
                children.Add(new StorageUsage(file.Name, file.Length, file.FullName));
            }
        }

        var programs = programCatalog.ProgramsAt(path);
        if (programs.Count == 0 && totals.MainExecutable is { } executable && ReadProgram(executable) is { } program)
        {
            programs = [program];
        }

        return new EntryDetails(
            IsDirectory: true,
            Created: folder.CreationTime,
            LastModified: totals.LastModified,
            FileCount: totals.FileCount,
            FolderCount: totals.FolderCount,
            Composition: totals.Composition,
            Programs: programs,
            Children: children);
    }

    private long MeasureFolder(DirectoryInfo directory, FolderTotals totals, CancellationToken cancellationToken)
    {
        long sizeBytes = 0;
        try
        {
            foreach (var entry in directory.EnumerateFileSystemInfos("*", RecursiveOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry is FileInfo file)
                {
                    totals.AddFile(file);
                    sizeBytes += file.Length;
                }
                else
                {
                    totals.AddFolder();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not fully inspect {Directory}", directory.FullName);
        }

        return sizeBytes;
    }

    private ProgramInfo? ReadProgram(string executablePath)
    {
        try
        {
            var version = FileVersionInfo.GetVersionInfo(executablePath);
            var name = FirstNonEmpty(version.ProductName, version.FileDescription);
            if (name is null)
            {
                return null;
            }

            return new ProgramInfo(
                Name: name,
                Publisher: FirstNonEmpty(version.CompanyName),
                Version: FirstNonEmpty(version.ProductVersion, version.FileVersion),
                InstalledOn: null,
                Location: Path.GetDirectoryName(executablePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not read version information of {File}", executablePath);
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(value => value?.Trim()).FirstOrDefault(value => !string.IsNullOrEmpty(value));

    private sealed class FolderTotals
    {
        private readonly Dictionary<string, (long SizeBytes, long FileCount)> _byExtension = new(StringComparer.OrdinalIgnoreCase);
        private long _mainExecutableSize = -1;

        public long FileCount { get; private set; }

        public long FolderCount { get; private set; }

        public DateTimeOffset? LastModified { get; private set; }

        public string? MainExecutable { get; private set; }

        public IReadOnlyList<ExtensionUsage> Composition =>
            _byExtension.Select(entry => new ExtensionUsage(entry.Key, entry.Value.SizeBytes, entry.Value.FileCount)).ToList();

        public void AddFolder() => FolderCount++;

        public void AddFile(FileInfo file)
        {
            FileCount++;

            var (sizeBytes, count) = _byExtension.GetValueOrDefault(file.Extension);
            _byExtension[file.Extension] = (sizeBytes + file.Length, count + 1);

            if (LastModified is null || file.LastWriteTime > LastModified)
            {
                LastModified = file.LastWriteTime;
            }

            // The largest executable is usually the application itself rather than a helper or an updater.
            if (string.Equals(file.Extension, ".exe", StringComparison.OrdinalIgnoreCase) && file.Length > _mainExecutableSize)
            {
                _mainExecutableSize = file.Length;
                MainExecutable = file.FullName;
            }
        }
    }
}
