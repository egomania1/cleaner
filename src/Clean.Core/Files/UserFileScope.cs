namespace Clean.Core.Files;

// Where files may be listed and removed by hand: everywhere except Windows, installed programs'
// shared folders, application data and the hidden system folders of each drive.
public sealed class UserFileScope
{
    public const string VolumeArchiveFolderName = ".CleanArchive";

    private static readonly string[] HiddenSystemFolders =
        ["$Recycle.Bin", "System Volume Information", "Recovery", "$WinREAgent", "$SysReset", "$Windows.~BT", "$Windows.~WS", "Config.Msi", VolumeArchiveFolderName];

    private readonly List<string> _protectedFolders;

    public UserFileScope(IEnumerable<string> protectedFolders)
    {
        _protectedFolders = protectedFolders
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static UserFileScope ForCurrentUser(string archiveFolder)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new UserFileScope(
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Path.Combine(profile, "AppData"),
            archiveFolder,
        ]);
    }

    public bool IsExcludedFolder(string folder)
    {
        var path = Normalize(folder);
        if (HiddenSystemFolders.Any(name => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return _protectedFolders.Any(root => IsSameOrInside(path, root));
    }

    public bool Contains(string filePath)
    {
        var path = Normalize(filePath);
        var folder = Path.GetDirectoryName(path);
        if (folder is null || Path.GetPathRoot(path) is not { } root || string.Equals(Normalize(folder), Normalize(root), StringComparison.OrdinalIgnoreCase))
        {
            // Files straight at a drive root are pagefile.sys, hiberfil.sys and the like.
            return false;
        }

        var current = folder;
        while (current is not null && !string.Equals(Normalize(current), Normalize(root), StringComparison.OrdinalIgnoreCase))
        {
            if (IsExcludedFolder(current))
            {
                return false;
            }

            current = Path.GetDirectoryName(current);
        }

        return true;
    }

    private static bool IsSameOrInside(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
