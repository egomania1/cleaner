using Clean.Infrastructure.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Windows;

public class DiskServiceTests
{
    private readonly DiskService _service = new(NullLogger<DiskService>.Instance);

    [Fact]
    public async Task GetDisksAsync_ListsTheSystemDriveFirst()
    {
        var disks = await _service.GetDisksAsync(CancellationToken.None);

        Assert.NotEmpty(disks);
        Assert.True(disks[0].IsSystemDrive);
        Assert.Single(disks, disk => disk.IsSystemDrive);
    }

    [Fact]
    public async Task GetDisksAsync_ReturnsConsistentSizes()
    {
        var disks = await _service.GetDisksAsync(CancellationToken.None);

        Assert.All(disks, disk =>
        {
            Assert.True(disk.TotalBytes > 0);
            Assert.InRange(disk.FreeBytes, 0, disk.TotalBytes);
        });
    }

    [Fact]
    public async Task GetDisksAsync_StopsWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.GetDisksAsync(cancellation.Token));
    }
}
