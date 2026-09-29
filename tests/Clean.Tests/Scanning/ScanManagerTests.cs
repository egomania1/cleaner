using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Scanning;

namespace Clean.Tests.Scanning;

public class ScanManagerTests
{
    [Fact]
    public async Task RunAsync_AggregatesItemsFromEveryScanner()
    {
        var manager = new ScanManager(
        [
            new FakeScanner("temp", TestItems.Create(sizeBytes: 100)),
            new FakeScanner("browser", TestItems.Create(sizeBytes: 200), TestItems.Create(sizeBytes: 300)),
        ]);

        var result = await manager.RunAsync(null, CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(600, result.TotalBytes);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task RunAsync_KeepsGoing_WhenOneScannerFails()
    {
        var manager = new ScanManager(
        [
            new FailingScanner("broken"),
            new FakeScanner("temp", TestItems.Create(sizeBytes: 100)),
        ]);

        var result = await manager.RunAsync(null, CancellationToken.None);

        Assert.Single(result.Items);
        var error = Assert.Single(result.Errors);
        Assert.Equal("broken", error.ScannerId);
    }

    [Fact]
    public async Task RunAsync_StopsWhenCancelled()
    {
        var manager = new ScanManager([new FakeScanner("temp", TestItems.Create())]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.RunAsync(null, cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_ForwardsProgressFromScanners()
    {
        var manager = new ScanManager([new FakeScanner("temp", TestItems.Create())]);
        var progress = new SynchronousProgress<ScanProgress>();

        await manager.RunAsync(progress, CancellationToken.None);

        Assert.Contains(progress.Reports, report => report.CurrentScanner == "temp");
    }

    private sealed class FakeScanner(string id, params ScanItem[] items) : IScanner
    {
        public string Id => id;

        public string Name => id;

        public Task<IReadOnlyList<ScanItem>> ScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            progress?.Report(new ScanProgress(id, string.Empty, items.Length, 0, 0, TimeSpan.Zero));
            return Task.FromResult<IReadOnlyList<ScanItem>>(items);
        }
    }

    private sealed class FailingScanner(string id) : IScanner
    {
        public string Id => id;

        public string Name => id;

        public Task<IReadOnlyList<ScanItem>> ScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken) =>
            throw new IOException("Disk unavailable");
    }
}
