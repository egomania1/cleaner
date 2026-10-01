using Clean.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Scanning;

// Turns a rule path into an existing folder on the scanned drive, or nothing if it should not be looked at.
internal static class RuleFolders
{
    public static DirectoryInfo? Resolve(string rawPath, string driveRoot, IReparsePointDetector reparsePointDetector, ILogger logger)
    {
        var expanded = Environment.ExpandEnvironmentVariables(rawPath);
        if (expanded.Contains('%') || !Directory.Exists(expanded))
        {
            return null;
        }

        var folder = new DirectoryInfo(expanded);
        if (!string.Equals(
            Path.TrimEndingDirectorySeparator(folder.Root.FullName),
            Path.TrimEndingDirectorySeparator(driveRoot),
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // A link on the way could lead anywhere, for example to the user's documents.
        if (reparsePointDetector.FindLinkOnPath(folder.FullName) is { } link)
        {
            logger.LogWarning("Skipping {Folder}: {Link} is a link to another location", folder.FullName, link);
            return null;
        }

        return folder;
    }
}
