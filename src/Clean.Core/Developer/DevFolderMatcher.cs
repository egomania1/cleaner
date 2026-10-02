namespace Clean.Core.Developer;

public static class DevFolderMatcher
{
    // siblingNames: the names of the files in the folder that contains the candidate.
    public static DevFolderKind? Match(string folderName, IReadOnlyCollection<string> siblingNames) =>
        DevFolderKind.All.FirstOrDefault(kind =>
            string.Equals(kind.FolderName, folderName, StringComparison.OrdinalIgnoreCase)
            && HasSibling(kind.RequiredSibling, siblingNames));

    private static bool HasSibling(string required, IReadOnlyCollection<string> siblingNames)
    {
        if (required.Length == 0)
        {
            return true;
        }

        if (!required.StartsWith('*'))
        {
            return siblingNames.Contains(required, StringComparer.OrdinalIgnoreCase);
        }

        var extension = required[1..];
        return siblingNames.Any(name => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}
