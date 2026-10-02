using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Safety;
using Clean.Core.Storage;
using Clean.Core.Uninstall;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Uninstall;

// Looks for folders an uninstaller left behind, by name only: never the registry, never a folder that
// another installed program uses, and never a protected folder or one reached through a link.
public sealed class LeftoverFinder(
    IInstalledProgramCatalog catalog,
    IReparsePointDetector reparsePointDetector,
    ProtectedPathService protectedPaths,
    IReadOnlyList<string> roots,
    ILogger<LeftoverFinder> logger) : ILeftoverFinder
{
    private static readonly EnumerationOptions Options = new() { IgnoreInaccessible = true, AttributesToSkip = 0 };

    public static IReadOnlyList<string> DefaultRoots() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
    ];

    // Links are never followed, in the folder or below it.
    public static IEnumerable<FileInfo> EnumerateFiles(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            List<FileSystemInfo> entries;
            try
            {
                entries = folder.EnumerateFileSystemInfos("*", Options).ToList();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (entry is DirectoryInfo directory)
                {
                    pending.Push(directory);
                }
                else
                {
                    yield return (FileInfo)entry;
                }
            }
        }
    }

    public Task<LeftoverSearch> FindAsync(ProgramInfo program, CancellationToken cancellationToken) =>
        Task.Run(() => Find(program, cancellationToken), cancellationToken);

    private LeftoverSearch Find(ProgramInfo program, CancellationToken cancellationToken)
    {
        catalog.Reload();
        if (catalog.All.Any(installed => string.Equals(installed.Name, program.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return new LeftoverSearch(true, []);
        }

        var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = LeftoverNames.For(program);
        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            foreach (var name in names)
            {
                candidates.TryAdd(Path.Combine(root, name), "Dossier au nom de l'application dans " + Path.GetFileName(root.TrimEnd('\\')));
            }
        }

        if (!string.IsNullOrWhiteSpace(program.Location))
        {
            candidates.TryAdd(program.Location.Trim().Trim('"').TrimEnd('\\', '/'), "Dossier d'installation de l'application");
        }

        var found = new List<LeftoverFolder>();
        foreach (var (path, reason) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsEligible(path) && Measure(path, reason, cancellationToken) is { FileCount: > 0 } folder)
            {
                found.Add(folder);
            }
        }

        return new LeftoverSearch(false, found.OrderByDescending(folder => folder.SizeBytes).ToList());
    }

    private bool IsEligible(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.Parent is null)
            {
                return false;
            }

            var full = info.FullName.TrimEnd('\\');
            var appFolder = AppContext.BaseDirectory.TrimEnd('\\');
            var holdsThisApp = string.Equals(appFolder, full, StringComparison.OrdinalIgnoreCase)
                || appFolder.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase);
            return !holdsThisApp
                && protectedPaths.FindProtectedFolderWithin(full) is null
                && reparsePointDetector.FindLinkOnPath(full) is null
                && ProgramMatcher.ProgramsAt(catalog.All, full).Count == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            logger.LogDebug(exception, "Skipping the possible leftover {Path}", path);
            return false;
        }
    }

    private static LeftoverFolder Measure(string path, string reason, CancellationToken cancellationToken)
    {
        long bytes = 0;
        long count = 0;
        foreach (var file in EnumerateFiles(path, cancellationToken))
        {
            bytes += file.Length;
            count++;
        }

        return new LeftoverFolder(path, bytes, count, reason);
    }
}
