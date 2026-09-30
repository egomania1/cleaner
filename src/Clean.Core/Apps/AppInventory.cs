using Clean.Core.Models;

namespace Clean.Core.Apps;

// MeasureFolder is null when the app has no folder of its own; its size then comes from the installer's estimate.
public sealed record InventoryEntry(
    string Key,
    ProgramInfo Program,
    string? MeasureFolder,
    IReadOnlyList<string> ExcludedFolders,
    string? SharesFolderWith);

public static class AppInventory
{
    public static string KeyOf(ProgramInfo program) => $"app:{program.Name}";

    public static IReadOnlyList<InventoryEntry> Build(IEnumerable<ProgramInfo> programs, IEnumerable<string> sharedFolders)
    {
        var shared = sharedFolders.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 32-bit and 64-bit registry views often list the same app twice; the most complete entry wins.
        var unique = programs
            .GroupBy(program => program.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(program => program.Location is not null)
                .ThenByDescending(program => program.EstimatedSizeBytes ?? 0)
                .First())
            .ToList();

        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<(ProgramInfo Program, string? Folder, string? SharesWith)>();

        // The biggest declared app owns a folder shared by several entries (an app and its helper tool).
        foreach (var program in unique.OrderByDescending(program => program.EstimatedSizeBytes ?? 0).ThenBy(program => program.Name, StringComparer.OrdinalIgnoreCase))
        {
            var folder = OwnFolder(program, shared);
            if (folder is not null && owners.TryGetValue(folder, out var owner))
            {
                entries.Add((program, null, owner));
                continue;
            }

            if (folder is not null)
            {
                owners[folder] = program.Name;
            }

            entries.Add((program, folder, null));
        }

        var folders = entries.Where(entry => entry.Folder is not null).Select(entry => entry.Folder!).ToList();
        return entries
            .Select(entry => new InventoryEntry(
                KeyOf(entry.Program),
                entry.Program,
                entry.Folder,
                entry.Folder is null ? [] : folders.Where(other => IsInside(other, entry.Folder)).ToList(),
                entry.SharesWith))
            .ToList();
    }

    private static string? OwnFolder(ProgramInfo program, HashSet<string> shared)
    {
        if (string.IsNullOrWhiteSpace(program.Location))
        {
            return null;
        }

        var folder = Normalize(program.Location);

        // A drive root or a folder shared by many apps ("Program Files") is not the app's own folder.
        return folder.Length <= 3 || shared.Contains(folder) ? null : folder;
    }

    private static bool IsInside(string candidate, string folder) =>
        candidate.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => path.Trim().Trim('"').TrimEnd('\\', '/').Replace('/', '\\');
}
