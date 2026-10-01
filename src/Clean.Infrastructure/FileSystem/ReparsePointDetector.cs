using Clean.Core.Interfaces;

namespace Clean.Infrastructure.FileSystem;

// Checking only the last folder is not enough: if any parent is a junction, the whole path
// leads somewhere else, for example "AppData\Local\App" pointing to the user's documents.
public sealed class ReparsePointDetector : IReparsePointDetector
{
    public string? FindLinkOnPath(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var current = root;

        foreach (var segment in full[root.Length..].Split('\\',StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                // Nothing exists from here on, so nothing further can redirect the path.
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be inspected cannot be trusted either.
                return current;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return current;
            }
        }

        return null;
    }
}
