using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.FileSystem;

public sealed class StorageAnalyzerTests : IDisposable
{
    private readonly TestDirectory _disk = new();
    private readonly StorageAnalyzer _analyzer = new(NullLogger<StorageAnalyzer>.Instance);

    public void Dispose() => _disk.Dispose();

    [Fact]
    public async Task AnalyzeAsync_SumsEachTopLevelFolderRecursively()
    {
        _disk.CreateFile(@"Games\game.bin", 100);
        _disk.CreateFile(@"Games\Saves\save.bin", 50);
        _disk.CreateFile("notes.txt", 10);

        var usages = await _analyzer.AnalyzeAsync(_disk.RootPath, null, CancellationToken.None);

        Assert.Equal(150, SizeOf(usages, "Games"));
        Assert.Equal(10, SizeOf(usages, "notes.txt"));
    }

    [Fact]
    public async Task AnalyzeAsync_NeverFollowsJunctions()
    {
        var outside = new TestDirectory();
        try
        {
            outside.CreateFile("Documents/huge.bin", 5_000);
            _disk.CreateFile(@"Cache\cache.bin", 20);
            _disk.CreateJunction(@"Cache\DocumentsLink", Path.Combine(outside.RootPath, "Documents"));
            _disk.CreateJunction("TopLevelLink", Path.Combine(outside.RootPath, "Documents"));

            var usages = await _analyzer.AnalyzeAsync(_disk.RootPath, null, CancellationToken.None);

            Assert.Equal(20, SizeOf(usages, "Cache"));
            Assert.DoesNotContain(usages, usage => usage.Label == "TopLevelLink");
        }
        finally
        {
            outside.Dispose();
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ReportsFilesAndBytes()
    {
        _disk.CreateFile(@"A\one.bin", 30);
        _disk.CreateFile(@"B\two.bin", 70);
        var progress = new SynchronousProgress<ScanProgress>();

        await _analyzer.AnalyzeAsync(_disk.RootPath, progress, CancellationToken.None);

        var last = progress.Reports[^1];
        Assert.Equal(2, last.FilesScanned);
        Assert.Equal(100, last.BytesAnalyzed);
    }

    [Fact]
    public async Task AnalyzeAsync_StopsWhenCancelled()
    {
        _disk.CreateFile("file.bin", 10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _analyzer.AnalyzeAsync(_disk.RootPath, null, cancellation.Token));
    }

    private static long SizeOf(IReadOnlyList<StorageUsage> usages, string label) =>
        Assert.Single(usages, usage => usage.Label == label).SizeBytes;
}
