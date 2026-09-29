using Clean.Core.Models;
using Clean.Infrastructure.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Scanning;

public sealed class KnownLocationScannerTests : IDisposable
{
    private static readonly DateTime OldDate = DateTime.UtcNow.AddDays(-30);

    private readonly TestDirectory _cache = new();

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task ScanAsync_MeasuresOnlyFilesOlderThanTheMinimumAge()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("old.tmp", 100), OldDate);
        File.SetLastWriteTimeUtc(_cache.CreateFile(@"sub\old2.tmp", 50), OldDate);
        _cache.CreateFile("recent.tmp", 999);

        var item = Assert.Single(await ScanAsync(Rule(minimumAgeDays: 2)));

        Assert.Equal(150, item.SizeBytes);
        Assert.Equal(2, item.FileCount);
        Assert.Equal(1, item.SkippedFileCount);
        Assert.True(item.CanClean);
    }

    [Fact]
    public async Task ScanAsync_DescribesTheItemWithItsRule()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("shader.bin", 10), OldDate);

        var item = Assert.Single(await ScanAsync(Rule(risk: RiskLevel.Caution)));

        Assert.Equal("TEST_CACHE", item.RuleId);
        Assert.Equal("Test cache", item.Name);
        Assert.Equal("Rebuilt automatically.", item.Reason);
        Assert.Equal(_cache.RootPath, item.Path);
        Assert.True(item.RequiresConfirmation);
    }

    [Fact]
    public async Task ScanAsync_IgnoresMissingAndUnresolvedPaths()
    {
        var rule = Rule() with { Paths = [@"C:\Clean\Does\Not\Exist", @"%CLEAN_UNDEFINED_VARIABLE%\Cache"] };

        Assert.Empty(await ScanAsync(rule));
    }

    [Fact]
    public async Task ScanAsync_NeverFollowsJunctionsInsideARuleFolder()
    {
        var documents = new TestDirectory();
        try
        {
            File.SetLastWriteTimeUtc(documents.CreateFile("thesis.docx", 5_000), OldDate);
            File.SetLastWriteTimeUtc(_cache.CreateFile("cache.bin", 10), OldDate);
            _cache.CreateJunction("DocumentsLink", documents.RootPath);

            var item = Assert.Single(await ScanAsync(Rule()));

            Assert.Equal(10, item.SizeBytes);
        }
        finally
        {
            documents.Dispose();
        }
    }

    [Fact]
    public async Task ScanAsync_RefusesARuleFolderThatIsItselfAJunction()
    {
        var documents = new TestDirectory();
        try
        {
            File.SetLastWriteTimeUtc(documents.CreateFile("thesis.docx", 5_000), OldDate);
            _cache.CreateJunction("LinkedCache", documents.RootPath);
            var rule = Rule() with { Paths = [Path.Combine(_cache.RootPath, "LinkedCache")] };

            Assert.Empty(await ScanAsync(rule));
        }
        finally
        {
            documents.Dispose();
        }
    }

    [Fact]
    public async Task ScanAsync_MeasuresAFolderListedTwiceOnlyOnce()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("a.tmp", 10), OldDate);
        var rule = Rule() with { Paths = [_cache.RootPath, _cache.RootPath + "\\"] };

        Assert.Single(await ScanAsync(rule));
    }

    private static Task<IReadOnlyList<ScanItem>> ScanAsync(CleaningRule rule) =>
        new KnownLocationScanner([rule], TimeProvider.System, NullLogger<KnownLocationScanner>.Instance)
            .ScanAsync(null, CancellationToken.None);

    private CleaningRule Rule(int minimumAgeDays = 0, RiskLevel risk = RiskLevel.Safe) =>
        new("TEST_CACHE", "Test cache", "Rebuilt automatically.", CleaningCategory.ApplicationCache, risk,
            [_cache.RootPath], minimumAgeDays, AutomaticCleaningAllowed: true);
}
