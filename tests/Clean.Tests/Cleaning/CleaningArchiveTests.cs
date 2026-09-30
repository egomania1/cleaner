using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;
using Clean.Infrastructure.Cleaning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Cleaning;

public sealed class CleaningArchiveTests : IDisposable
{
    private static readonly DateTime OldDate = DateTime.UtcNow.AddDays(-30);

    private readonly TestDirectory _cache = new();
    private readonly TestDirectory _archiveRoot = new();
    private readonly TestClock _clock = new(DateTimeOffset.UtcNow);
    private readonly CleaningArchive _archive;

    public CleaningArchiveTests()
    {
        _archive = new CleaningArchive(_archiveRoot.RootPath, _clock, NullLogger<CleaningArchive>.Instance);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _archiveRoot.Dispose();
    }

    [Fact]
    public async Task RestoreAsync_PutsEveryFileBackWithItsContentAndDate()
    {
        var file = Old(_cache.CreateFile(@"deep\folder\cache.bin", 64));
        File.WriteAllText(file, "original content");
        File.SetLastWriteTimeUtc(file, OldDate);
        var session = await CleanAsync();
        Assert.False(File.Exists(file));

        var result = await _archive.RestoreAsync(session.Id, CancellationToken.None);

        Assert.Equal(1, result.RestoredFileCount);
        Assert.Equal("original content", File.ReadAllText(file));
        Assert.Equal(OldDate, File.GetLastWriteTimeUtc(file), TimeSpan.FromSeconds(1));
        Assert.Equal(CleaningSessionStatus.Restored, (await SingleSessionAsync()).Status);
        Assert.False(Directory.Exists(Path.Combine(_archiveRoot.RootPath, "sessions", session.Id)));
    }

    [Fact]
    public async Task RestoreAsync_KeepsAFileTheApplicationRecreatedMeanwhile()
    {
        var file = Old(_cache.CreateFile("settings.cache", 10));
        var session = await CleanAsync();
        File.WriteAllText(file, "new version");

        var result = await _archive.RestoreAsync(session.Id, CancellationToken.None);

        Assert.Equal(1, result.ConflictFileCount);
        Assert.Equal("new version", File.ReadAllText(file));
    }

    [Fact]
    public async Task RestoreAsync_DoesNothingTwice()
    {
        Old(_cache.CreateFile("a.tmp", 10));
        var session = await CleanAsync();
        await _archive.RestoreAsync(session.Id, CancellationToken.None);

        var second = await _archive.RestoreAsync(session.Id, CancellationToken.None);

        Assert.Equal(0, second.RestoredFileCount);
    }

    [Fact]
    public async Task FreeAsync_DeletesTheArchivedFilesAndEndsTheSession()
    {
        Old(_cache.CreateFile("a.tmp", 100));
        var readOnly = Old(_cache.CreateFile("b.tmp", 20));
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        var session = await CleanAsync();

        var freed = await _archive.FreeAsync(session.Id, CancellationToken.None);

        Assert.Equal(120, freed);
        Assert.False(Directory.Exists(Path.Combine(_archiveRoot.RootPath, "sessions", session.Id)));
        var stored = await SingleSessionAsync();
        Assert.Equal(CleaningSessionStatus.Freed, stored.Status);
        Assert.False(stored.CanRestore);
        Assert.Equal(0, (await _archive.RestoreAsync(session.Id, CancellationToken.None)).RestoredFileCount);
    }

    [Fact]
    public async Task FreeExpiredAsync_OnlyFreesSessionsPastTheRestorePeriod()
    {
        Old(_cache.CreateFile("a.tmp", 10));
        await CleanAsync();

        Assert.Equal(0, await _archive.FreeExpiredAsync(CancellationToken.None));

        _clock.Now += CleaningSession.RetentionPeriod + TimeSpan.FromMinutes(1);
        Assert.Equal(10, await _archive.FreeExpiredAsync(CancellationToken.None));
        Assert.Equal(CleaningSessionStatus.Freed, (await SingleSessionAsync()).Status);
    }

    [Fact]
    public async Task History_SurvivesARestartOfTheApp()
    {
        Old(_cache.CreateFile("a.tmp", 10));
        var session = await CleanAsync();

        var reopened = new CleaningArchive(_archiveRoot.RootPath, _clock, NullLogger<CleaningArchive>.Instance);

        var stored = Assert.Single(await reopened.GetSessionsAsync(CancellationToken.None));
        Assert.Equal(session, stored with { Locations = session.Locations });
        Assert.Equal(session.Locations, stored.Locations);
    }

    [Fact]
    public async Task History_UnreadableFileIsKeptAside()
    {
        Directory.CreateDirectory(_archiveRoot.RootPath);
        File.WriteAllText(Path.Combine(_archiveRoot.RootPath, "history.json"), "{ not json");

        Assert.Empty(await _archive.GetSessionsAsync(CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(_archiveRoot.RootPath, "history.json.broken")));
    }

    [Theory]
    [InlineData(@"..\..")]
    [InlineData(@"C:\Windows")]
    [InlineData("")]
    public void GetLocationFolder_RejectsIdsThatLeaveTheArchive(string sessionId)
    {
        Assert.Throws<ArgumentException>(() => _archive.GetLocationFolder(sessionId, 0));
    }

    private static string Old(string path)
    {
        File.SetLastWriteTimeUtc(path, OldDate);
        return path;
    }

    private async Task<CleaningSession> SingleSessionAsync() =>
        Assert.Single(await _archive.GetSessionsAsync(CancellationToken.None));

    private async Task<CleaningSession> CleanAsync()
    {
        var rule = new CleaningRule("TEST_CACHE", "Test cache", "Rebuilt.", CleaningCategory.ApplicationCache, RiskLevel.Safe, [_cache.RootPath], 0, true);
        var engine = new RuleEngine([rule]);
        var safety = new SafetyEngine(new RulePathValidator(engine), engine);
        var cleaner = new FileCleaner(safety, engine, _archive, _clock, NullLogger<FileCleaner>.Instance);
        var item = new ScanItem(_cache.RootPath, rule.Name, 1, rule.Category, rule.Risk, rule.Description, rule.Id, null, true, false, 1, 0);

        var result = await cleaner.CleanAsync([safety.Evaluate(item)], null, CancellationToken.None);
        return result.Session!;
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
