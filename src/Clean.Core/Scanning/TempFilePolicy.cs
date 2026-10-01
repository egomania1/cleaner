using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Core.Scanning;

// Being in a Temp folder is not enough to be junk: files opened straight from an email or a zip
// live there too, and may be the only copy the user has.
public static class TempFilePolicy
{
    private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    private const FileAttributes CloudOnly = FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess;

    private static readonly HashSet<string> PersonalCategories = ["Documents", "Images", "Vidéos", "Musique"];

    public static KeptFileReason? FindReasonToKeep(string fileName, FileAttributes attributes, DateTime lastWriteUtc, DateTime cutoffUtc)
    {
        if (attributes.HasFlag(FileAttributes.System))
        {
            return KeptFileReason.SystemFile;
        }

        // Moving a cloud placeholder would first download it.
        if ((attributes & CloudOnly) != 0)
        {
            return KeptFileReason.CloudFile;
        }

        if (lastWriteUtc > cutoffUtc)
        {
            return KeptFileReason.TooRecent;
        }

        if (PersonalCategories.Contains(FileCategories.CategoryOf(Path.GetExtension(fileName))))
        {
            return KeptFileReason.PersonalFile;
        }

        return null;
    }
}
