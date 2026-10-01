using System.Text.Json;
using System.Text.Json.Nodes;
using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;
using Clean.Infrastructure.Cleaning;
using Clean.Infrastructure.FileSystem;
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
    public async Task RestoreAsync_NeverRestoresThroughALinkAndKeepsTheSessionRestorable()
    {
        using var elsewhere = new TestDirectory();
        Old(_cache.CreateFile("settings.cache", 10));
        var session = await CleanAsync();
        _cache.CreateJunction("Link", elsewhere.RootPath);
        var path = Path.Combine(_archiveRoot.RootPath, "history.json");
        var history = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        history[0]!["locations"]![0]!["path"] = Path.Combine(_cache.RootPath, "Link");
        File.WriteAllText(path, history.ToJsonString());

        var result = await _archive.RestoreAsync(session.Id, CancellationToken.None);

        Assert.Equal(0, result.RestoredFileCount);
        Assert.Equal(1, result.FailedFileCount);
        Assert.Empty(Directory.GetFiles(elsewhere.RootPath));
        Assert.Equal(CleaningSessionStatus.Restorable, (await SingleSessionAsync()).Status);
    }

    [Fact]
    public async Task History_UnreadableFileIsKeptAside()
    {
        Directory.CreateDirectory(_archiveRoot.RootPath);
        File.WriteAllText(Path.Combine(_archiveRoot.RootPath, "history.json"), "{ not json");

        Assert.Empty(await _archive.GetSessionsAsync(CancellationToken.None));
        Assert.True(File.Exists(Path.Combine(_archiveRoot.RootPath, "history.json.broken")));
    }

    [Fact]
    public async Task History_LeavesOutDamagedEntriesAndKeepsACopy()
    {
        Old(_cache.CreateFile("a.cache", 10));
        var good = await CleanAsync();
        var path = Path.Combine(_archiveRoot.RootPath, "history.json");
        var history = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        history.Add(JsonNode.Parse("""{"id":"..\\evil","locations":[]}"""));
        history.Add(JsonNode.Parse("""{"id":"no-locations","locations":null}"""));
        history.Add(JsonNode.Parse("""{"id":"relative-path","locations":[{"index":0,"path":"temp"}]}"""));
        history.Add(null);
        File.WriteAllText(path, history.ToJsonString());

        var sessions = await _archive.GetSessionsAsync(CancellationToken.None);

        Assert.Equal(good.Id, Assert.Single(sessions).Id);
        Assert.True(File.Exists(path + ".broken"));
    }

    [Fact]
    public async Task FreeExpiredAsync_KeepsFreeingWhenOneSessionCannotBeFreed()
    {
        Old(_cache.CreateFile("a.cache", 10));
        var good = await CleanAsync();
        var unusedDrive = Enumerable.Range('D', 'Z' - 'D' + 1).Select(letter => $"{(char)letter}:\\")
            .First(root => !Directory.Exists(root));
        var path = Path.Combine(_archiveRoot.RootPath, "history.json");
        var history = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        // Newer than the good session, so it is handled first; its drive does not exist.
        history.Add(JsonNode.Parse($$"""
            {"id":"bad-drive","cleanedAt":"2099-01-01T00:00:00+00:00","expiresAt":"2000-01-01T00:00:00+00:00",
             "status":"restorable","locations":[{"index":0,"ruleId":"X","name":"X","path":{{JsonSerializer.Serialize(unusedDrive + "Temp")}},"sizeBytes":1,"fileCount":1}]}
            """));
        File.WriteAllText(path, history.ToJsonString());
        _clock.Now = _clock.Now.AddDays(8);

        var freed = await _archive.FreeExpiredAsync(CancellationToken.None);

        Assert.Equal(10, freed);
        Assert.Equal(CleaningSessionStatus.Freed, (await _archive.GetSessionsAsync(CancellationToken.None)).Single(session => session.Id == good.Id).Status);
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
        var safety = new SafetyEngine(new PathValidator(engine, new ReparsePointDetector()), engine);
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
