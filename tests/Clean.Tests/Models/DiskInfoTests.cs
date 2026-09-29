using Clean.Core.Models;

namespace Clean.Tests.Models;

public class DiskInfoTests
{
    [Fact]
    public void UsedBytes_IsTotalMinusFree()
    {
        var disk = new DiskInfo(@"C:\", "System", DriveType.Fixed, TotalBytes: 1000, FreeBytes: 250);

        Assert.Equal(750, disk.UsedBytes);
        Assert.Equal(0.75, disk.UsedRatio);
    }

    [Fact]
    public void UsedRatio_IsZero_WhenDiskHasNoCapacity()
    {
        var disk = new DiskInfo(@"E:\", "Empty drive", DriveType.CDRom, TotalBytes: 0, FreeBytes: 0);

        Assert.Equal(0, disk.UsedRatio);
    }
}
