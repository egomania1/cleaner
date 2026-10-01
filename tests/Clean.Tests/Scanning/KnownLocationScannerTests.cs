using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
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

    [Fact]
    public async Task ScanAsync_IgnoresRuleFoldersOnOtherDrives()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("a.tmp", 10), OldDate);
        var otherDrive = Path.GetPathRoot(_cache.RootPath)!.StartsWith('Z') ? @"Y:\" : @"Z:\";

        Assert.Empty(await ScanAsync(Rule(), otherDrive));
    }

    [Fact]
    public async Task ScanAsync_WarnsWhenTheProgramOfTheRuleIsOpen()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("a.tmp", 10), OldDate);
        var rule = Rule() with { ProcessName = "somebrowser" };

        var item = Assert.Single(await ScanAsync(rule, isProcessRunning: name => name == "somebrowser"));

        Assert.Equal(RiskLevel.Caution, item.Risk);
        Assert.True(item.RequiresConfirmation);
        Assert.True(item.CanClean);
        Assert.StartsWith("Ce programme est ouvert", item.Reason);
    }

    [Fact]
    public async Task ScanAsync_KeepsTheRuleRiskWhenTheProgramIsClosedOrUnknown()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("a.tmp", 10), OldDate);

        var closed = Assert.Single(await ScanAsync(Rule() with { ProcessName = "somebrowser" }, isProcessRunning: _ => false));
        var unnamed = Assert.Single(await ScanAsync(Rule(), isProcessRunning: _ => true));

        Assert.Equal(RiskLevel.Safe, closed.Risk);
        Assert.Equal(RiskLevel.Safe, unnamed.Risk);
    }

    [Fact]
    public async Task ScanAsync_NeverLowersAHigherRisk()
    {
        File.SetLastWriteTimeUtc(_cache.CreateFile("a.tmp", 10), OldDate);

        var item = Assert.Single(await ScanAsync(Rule(risk: RiskLevel.Expert) with { ProcessName = "x" }, isProcessRunning: _ => true));

        Assert.Equal(RiskLevel.Expert, item.Risk);
    }

    private Task<IReadOnlyList<ScanItem>> ScanAsync(CleaningRule rule, string? driveRoot = null, Func<string, bool>? isProcessRunning = null) =>
        new KnownLocationScanner([rule], new ReparsePointDetector(), TimeProvider.System, NullLogger<KnownLocationScanner>.Instance, isProcessRunning)
            .ScanAsync(driveRoot ?? Path.GetPathRoot(_cache.RootPath)!, null, CancellationToken.None);

    private CleaningRule Rule(int minimumAgeDays = 0, RiskLevel risk = RiskLevel.Safe) =>
        new("TEST_CACHE", "Test cache", "Rebuilt automatically.", CleaningCategory.ApplicationCache, risk,
            [_cache.RootPath], minimumAgeDays, AutomaticCleaningAllowed: true);
}
