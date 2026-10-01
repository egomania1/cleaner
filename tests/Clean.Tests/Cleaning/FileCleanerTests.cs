using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;
using Clean.Infrastructure.Cleaning;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Cleaning;

public sealed class FileCleanerTests : IDisposable
{
    private static readonly DateTime OldDate = DateTime.UtcNow.AddDays(-30);

    private readonly TestDirectory _cache = new();
    private readonly TestDirectory _archiveRoot = new();
    private readonly CleaningArchive _archive;

    public FileCleanerTests()
    {
        _archive = new CleaningArchive(_archiveRoot.RootPath, TimeProvider.System, NullLogger<CleaningArchive>.Instance);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _archiveRoot.Dispose();
    }

    [Fact]
    public async Task CleanAsync_RemovesOldFilesAndKeepsRecentOnes()
    {
        var old = Old(_cache.CreateFile("old.tmp", 100));
        var oldNested = Old(_cache.CreateFile(@"sub\old2.tmp", 50));
        var recent = _cache.CreateFile("recent.tmp", 999);

        var result = await CleanAsync(Rule(minimumAgeDays: 2));

        Assert.False(File.Exists(old));
        Assert.False(File.Exists(oldNested));
        Assert.True(File.Exists(recent));
        Assert.Equal(150, result.RemovedBytes);
        Assert.Equal(2, result.RemovedFileCount);
        Assert.Equal(1, result.CleanedLocationCount);
        Assert.True(Directory.Exists(_cache.RootPath));
    }

    [Fact]
    public async Task CleanAsync_MovesFilesToTheArchiveAndRecordsTheSession()
    {
        Old(_cache.CreateFile(@"sub\old.tmp", 42));

        var result = await CleanAsync(Rule());

        var session = Assert.Single(await _archive.GetSessionsAsync(CancellationToken.None));
        Assert.Equal(result.Session?.Id, session.Id);
        Assert.Equal(42, session.SizeBytes);
        Assert.Equal(CleaningSessionStatus.Restorable, session.Status);
        var location = Assert.Single(session.Locations);
        Assert.Equal(_cache.RootPath, location.Path);
        Assert.True(File.Exists(Path.Combine(_archive.GetLocationFolder(session.Id, location.Index), "sub", "old.tmp")));
    }

    [Fact]
    public async Task CleanAsync_RecordsNothingWhenNothingWasRemoved()
    {
        _cache.CreateFile("recent.tmp", 10);

        var result = await CleanAsync(Rule(minimumAgeDays: 2));

        Assert.Null(result.Session);
        Assert.Empty(await _archive.GetSessionsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CleanAsync_KeepsFilesWrittenSinceTheScan()
    {
        var file = Old(_cache.CreateFile("cache.bin", 10));
        var rule = Rule(minimumAgeDays: 2);
        var decision = Evaluate(rule);
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow);

        var result = await Cleaner(rule).CleanAsync([decision], null, CancellationToken.None);

        Assert.True(File.Exists(file));
        Assert.Equal(1, result.KeptRecentFileCount);
        Assert.Equal(0, result.RemovedFileCount);
    }

    [Fact]
    public async Task CleanAsync_RemovesReadOnlyFiles()
    {
        var file = Old(_cache.CreateFile("readonly.tmp", 10));
        File.SetAttributes(file, FileAttributes.ReadOnly);

        await CleanAsync(Rule());

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task CleanAsync_KeepsPersonalFilesInTemporaryFolders()
    {
        var leftover = Old(_cache.CreateFile("setup.log", 10));
        var attachment = Old(_cache.CreateFile("contrat.pdf", 10));

        var result = await CleanAsync(Rule() with { Category = CleaningCategory.Temporary });

        Assert.False(File.Exists(leftover));
        Assert.True(File.Exists(attachment));
        Assert.Equal(1, result.RemovedFileCount);
    }

    [Fact]
    public async Task CleanAsync_CountsFilesHeldOpenAsLockedAndKeepsThem()
    {
        var file = Old(_cache.CreateFile("open.log", 10));

        CleaningResult result;
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await CleanAsync(Rule());
        }

        Assert.True(File.Exists(file));
        Assert.Equal(1, result.LockedFileCount);
    }

    [Fact]
    public async Task CleanAsync_NeverFollowsJunctionsInsideARuleFolder()
    {
        var documents = new TestDirectory();
        try
        {
            var thesis = Old(documents.CreateFile("thesis.docx", 5_000));
            Old(_cache.CreateFile("cache.bin", 10));
            _cache.CreateJunction("DocumentsLink", documents.RootPath);

            await CleanAsync(Rule());

            Assert.True(File.Exists(thesis));
            Assert.True(Directory.Exists(Path.Combine(_cache.RootPath, "DocumentsLink")));
        }
        finally
        {
            documents.Dispose();
        }
    }

    [Fact]
    public async Task CleanAsync_RefusesARuleFolderThatIsItselfAJunction()
    {
        var documents = new TestDirectory();
        try
        {
            var thesis = Old(documents.CreateFile("thesis.docx", 5_000));
            _cache.CreateJunction("Cache", documents.RootPath);
            var rule = Rule() with { Paths = [Path.Combine(_cache.RootPath, "Cache")] };

            var result = await CleanAsync(rule);

            Assert.True(File.Exists(thesis));
            Assert.Equal(0, result.RemovedFileCount);
        }
        finally
        {
            documents.Dispose();
        }
    }

    [Fact]
    public async Task CleanAsync_RefusesARuleFolderReachedThroughAJunctionedParent()
    {
        var documents = new TestDirectory();
        try
        {
            var thesis = Old(documents.CreateFile(@"Cache\thesis.docx", 5_000));
            _cache.CreateJunction("App", documents.RootPath);
            var rule = Rule() with { Paths = [Path.Combine(_cache.RootPath, "App", "Cache")] };
            var forged = CleaningDecision.Allow(Scanned(rule), "Forged");

            var result = await Cleaner(rule).CleanAsync([forged], null, CancellationToken.None);

            Assert.True(File.Exists(thesis));
            Assert.Equal(0, result.RemovedFileCount);
        }
        finally
        {
            documents.Dispose();
        }
    }

    [Fact]
    public async Task CleanAsync_RemovesOldEmptySubfoldersButKeepsNewOnes()
    {
        Old(_cache.CreateFile(@"old\a.tmp", 10));
        Directory.SetCreationTimeUtc(Path.Combine(_cache.RootPath, "old"), OldDate);
        var fresh = _cache.CreateDirectory("fresh");

        await CleanAsync(Rule(minimumAgeDays: 2));

        Assert.False(Directory.Exists(Path.Combine(_cache.RootPath, "old")));
        Assert.True(Directory.Exists(fresh));
    }

    [Fact]
    public async Task CleanAsync_IgnoresDeniedDecisions()
    {
        var file = Old(_cache.CreateFile("cache.bin", 10));
        var rule = Rule();

        var result = await Cleaner(rule).CleanAsync([CleaningDecision.Deny(Scanned(rule), "No")], null, CancellationToken.None);

        Assert.True(File.Exists(file));
        Assert.Equal(0, result.CleanedLocationCount);
    }

    [Fact]
    public async Task CleanAsync_RefusesAnAllowedDecisionOutsideTheRuleFolders()
    {
        var other = new TestDirectory();
        try
        {
            var file = Old(other.CreateFile("precious.docx", 10));
            var rule = Rule();
            var forged = CleaningDecision.Allow(Scanned(rule) with { Path = other.RootPath }, "Forged");

            await Cleaner(rule).CleanAsync([forged], null, CancellationToken.None);

            Assert.True(File.Exists(file));
        }
        finally
        {
            other.Dispose();
        }
    }

    [Fact]
    public async Task CleanAsync_StopsWhenCancelledAndReportsIt()
    {
        var file = Old(_cache.CreateFile("cache.bin", 10));
        var rule = Rule();
        var decision = Evaluate(rule);

        var result = await Cleaner(rule).CleanAsync([decision], null, new CancellationToken(canceled: true));

        Assert.True(File.Exists(file));
        Assert.True(result.WasCancelled);
    }

    private static string Old(string path)
    {
        File.SetLastWriteTimeUtc(path, OldDate);
        return path;
    }

    private CleaningRule Rule(int minimumAgeDays = 0) =>
        new("TEST_CACHE", "Test cache", "Rebuilt automatically.", CleaningCategory.ApplicationCache, RiskLevel.Safe, [_cache.RootPath], minimumAgeDays, true);

    private static ScanItem Scanned(CleaningRule rule) =>
        new(rule.Paths[0], rule.Name, 1, rule.Category, rule.Risk, rule.Description, rule.Id, null, true, false, 1, 0);

    private CleaningDecision Evaluate(CleaningRule rule)
    {
        var engine = new RuleEngine([rule]);
        return new SafetyEngine(new PathValidator(engine, new ReparsePointDetector()), engine).Evaluate(Scanned(rule));
    }

    private FileCleaner Cleaner(CleaningRule rule)
    {
        var engine = new RuleEngine([rule]);
        return new FileCleaner(new SafetyEngine(new PathValidator(engine, new ReparsePointDetector()), engine), engine, _archive, TimeProvider.System, NullLogger<FileCleaner>.Instance);
    }

    private Task<CleaningResult> CleanAsync(CleaningRule rule) =>
        Cleaner(rule).CleanAsync([Evaluate(rule)], null, CancellationToken.None);
}
