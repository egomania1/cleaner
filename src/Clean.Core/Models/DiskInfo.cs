namespace Clean.Core.Models;

public sealed record DiskInfo(
    string RootPath,
    string Label,
    DriveType Type,
    long TotalBytes,
    long FreeBytes)
{
    public long UsedBytes => TotalBytes - FreeBytes;

    public double UsedRatio => TotalBytes > 0 ? (double)UsedBytes / TotalBytes : 0;
}
