using Clean.Core.Interfaces;
using Clean.Core.Models;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Windows;

public sealed class DiskService(ILogger<DiskService> logger) : IDiskService
{
    public Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken) =>
        Task.Run(() => ReadDisks(cancellationToken), cancellationToken);

    private IReadOnlyList<DiskInfo> ReadDisks(CancellationToken cancellationToken)
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        var disks = new List<DiskInfo>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var disk = TryRead(drive, systemRoot);
            if (disk is not null)
            {
                disks.Add(disk);
            }
        }

        return disks
            .OrderByDescending(disk => disk.IsSystemDrive)
            .ThenBy(disk => disk.RootPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private DiskInfo? TryRead(DriveInfo drive, string? systemRoot)
    {
        try
        {
            // An empty card reader or optical drive is listed but has no readable volume.
            if (!drive.IsReady)
            {
                return null;
            }

            return new DiskInfo(
                RootPath: drive.RootDirectory.FullName,
                Label: drive.VolumeLabel,
                Type: drive.DriveType,
                TotalBytes: drive.TotalSize,
                FreeBytes: drive.AvailableFreeSpace,
                IsSystemDrive: string.Equals(drive.Name, systemRoot, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not read drive {Drive}", drive.Name);
            return null;
        }
    }
}
