namespace Clean.Core.Safety;

// Folders that may contain a known cache deeper down, but that must never be cleaned as a whole.
public sealed class ProtectedPathService
{
    private static readonly string[] ProtectedFolders =
    [
        @"%SystemDrive%\",
        @"%SystemDrive%\Users",
        "%WINDIR%",
        @"%WINDIR%\System32",
        "%ProgramFiles%",
        "%ProgramFiles(x86)%",
        "%PROGRAMDATA%",
        "%PUBLIC%",
        "%USERPROFILE%",
        "%APPDATA%",
        "%LOCALAPPDATA%",
        @"%USERPROFILE%\Desktop",
        @"%USERPROFILE%\Documents",
        @"%USERPROFILE%\Downloads",
        @"%USERPROFILE%\Pictures",
        @"%USERPROFILE%\Videos",
        @"%USERPROFILE%\Music",
        "%OneDrive%",
    ];

    private readonly IReadOnlyList<string> _folders;

    public ProtectedPathService(Func<string, string>? expand = null)
    {
        var expander = expand ?? Environment.ExpandEnvironmentVariables;
        _folders = ProtectedFolders
            .Select(expander)
            .Where(folder => !folder.Contains('%'))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Returns the protected folder that cleaning this path would wipe: the path itself or one inside it.
    public string? FindProtectedFolderWithin(string path)
    {
        var target = Normalize(path);
        return _folders.FirstOrDefault(folder => IsSameOrInside(folder, target));
    }

    // "C:" alone means "the current folder on C:", so a bare drive name is read as its root.
    public static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(IsDriveName(path) ? path + '\\' : path));

    private static bool IsDriveName(string path) => path.Length == 2 && path[1] == ':';

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(Path.EndsInDirectorySeparator(folder) ? folder : folder + '\\', StringComparison.OrdinalIgnoreCase);
}
