using Clean.Core.Formatting;
using Clean.Core.Models;

namespace Clean.App.ViewModels;

public sealed class DiskItemViewModel(DiskInfo disk)
{
    public string Name => disk.RootPath.TrimEnd('\\');

    public string Label => string.IsNullOrWhiteSpace(disk.Label) ? DescribeType(disk.Type) : disk.Label;

    public bool IsSystemDrive => disk.IsSystemDrive;

    public double UsedRatio => disk.UsedRatio;

    public string UsageText => $"{ByteSize.Format(disk.UsedBytes)} / {ByteSize.Format(disk.TotalBytes)}";

    public string FreeText => $"{ByteSize.Format(disk.FreeBytes)} disponibles";

    private static string DescribeType(DriveType type) => type switch
    {
        DriveType.Fixed => "Disque local",
        DriveType.Removable => "Disque amovible",
        DriveType.Network => "Lecteur réseau",
        DriveType.CDRom => "Lecteur optique",
        _ => "Disque",
    };
}
