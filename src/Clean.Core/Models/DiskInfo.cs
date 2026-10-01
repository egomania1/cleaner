namespace Clean.Core.Models;

public sealed record DiskInfo(
    string RootPath,
    string Label,
    DriveType Type,
    long TotalBytes,
    long FreeBytes,
    bool IsSystemDrive)
{
    public long UsedBytes => TotalBytes - FreeBytes;

    public double UsedRatio => TotalBytes > 0 ? (double)UsedBytes / TotalBytes : 0;

    public bool CanRemoveFiles => CanRemoveFilesOn(Type);

    // Network shares have their own rights and recycle bin, and optical or RAM drives are not ours to touch.
    public static bool CanRemoveFilesOn(DriveType type) => type is DriveType.Fixed or DriveType.Removable;
}
