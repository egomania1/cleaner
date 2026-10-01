using Clean.Core.Models;

namespace Clean.Tests.Models;

public class DiskInfoTests
{
    [Fact]
    public void UsedBytes_IsTotalMinusFree()
    {
        var disk = new DiskInfo(@"C:\", "System", DriveType.Fixed, TotalBytes: 1000, FreeBytes: 250, IsSystemDrive: true);

        Assert.Equal(750, disk.UsedBytes);
        Assert.Equal(0.75, disk.UsedRatio);
    }

    [Theory]
    [InlineData(DriveType.Fixed, true)]
    [InlineData(DriveType.Removable, true)]
    [InlineData(DriveType.Network, false)]
    [InlineData(DriveType.CDRom, false)]
    [InlineData(DriveType.Ram, false)]
    [InlineData(DriveType.NoRootDirectory, false)]
    [InlineData(DriveType.Unknown, false)]
    public void CanRemoveFiles_OnlyOnLocalDisks(DriveType type, bool expected)
    {
        var disk = new DiskInfo(@"E:\", "Disk", type, TotalBytes: 100, FreeBytes: 50, IsSystemDrive: false);

        Assert.Equal(expected, disk.CanRemoveFiles);
    }

    [Fact]
    public void UsedRatio_IsZero_WhenDiskHasNoCapacity()
    {
        var disk = new DiskInfo(@"E:\", "Empty drive", DriveType.CDRom, TotalBytes: 0, FreeBytes: 0, IsSystemDrive: false);

        Assert.Equal(0, disk.UsedRatio);
    }
}
