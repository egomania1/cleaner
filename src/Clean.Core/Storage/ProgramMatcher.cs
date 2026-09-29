using Clean.Core.Models;

namespace Clean.Core.Storage;

public static class ProgramMatcher
{
    // Programs installed inside the folder come first (a vendor folder such as "Program Files\Google");
    // otherwise the closest program whose install folder contains it (a subfolder of an application).
    public static IReadOnlyList<ProgramInfo> ProgramsAt(IEnumerable<ProgramInfo> programs, string path)
    {
        var target = Normalize(path);
        // Some installers register a whole drive ("F:\") as their location, which would claim every folder on it.
        var located = programs
            .Where(program => !string.IsNullOrWhiteSpace(program.Location))
            .Select(program => (Program: program, Location: Normalize(program.Location!)))
            .Where(candidate => !IsDriveRoot(candidate.Location))
            .ToList();

        var inside = located
            .Where(candidate => IsSameOrInside(candidate.Location, target))
            .Select(candidate => candidate.Program)
            .DistinctBy(program => program.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(program => program.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (inside.Count > 0)
        {
            return inside;
        }

        return located
            .Where(candidate => IsSameOrInside(target, candidate.Location))
            .OrderByDescending(candidate => candidate.Location.Length)
            .Select(candidate => candidate.Program)
            .Take(1)
            .ToList();
    }

    private static bool IsSameOrInside(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
        || candidate.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);

    private static bool IsDriveRoot(string location) => location.Length <= 2 && location.EndsWith(':');

    private static string Normalize(string path) => path.Trim().Trim('"').TrimEnd('\\', '/').Replace('/', '\\');
}
