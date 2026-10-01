using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
using Clean.Infrastructure.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Scanning;

public sealed class TempScannerTests : IDisposable
{
    private static readonly DateTime OldDate = DateTime.UtcNow.AddDays(-30);

    private readonly TestDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task ScanAsync_CountsOnlyOldLeftoversAndExplainsTheRest()
    {
        Old(_temp.CreateFile("setup.log", 100));
        Old(_temp.CreateFile(@"sub\installer.exe", 50));
        _temp.CreateFile("recent.tmp", 7);
        Old(_temp.CreateFile("attachment.pdf", 20));

        var item = Assert.Single(await ScanAsync());

        Assert.Equal(150, item.SizeBytes);
        Assert.Equal(2, item.FileCount);
        Assert.Equal(2, item.SkippedFileCount);
        Assert.Equal(
            [new KeptFiles(KeptFileReason.TooRecent, 1, 7), new KeptFiles(KeptFileReason.PersonalFile, 1, 20)],
            item.KeptFiles);
    }

    [Fact]
    public async Task ScanAsync_ReportsFilesHeldOpenAsInUse()
    {
        var file = Old(_temp.CreateFile("locked.tmp", 30));

        ScanItem item;
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            item = Assert.Single(await ScanAsync());
        }

        Assert.Equal(0, item.SizeBytes);
        Assert.False(item.CanClean);
        Assert.Equal([new KeptFiles(KeptFileReason.InUse, 1, 30)], item.KeptFiles);
    }

    [Fact]
    public async Task ScanAsync_CountsFilesOpenedByAnAppThatAllowsTheirRemoval()
    {
        var file = Old(_temp.CreateFile("shared.tmp", 30));

        ScanItem item;
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            item = Assert.Single(await ScanAsync());
        }

        Assert.Equal(30, item.SizeBytes);
        Assert.Empty(item.KeptFiles!);
    }

    [Fact]
    public async Task ScanAsync_MeasuresAFolderNamedTwiceOnlyOnce()
    {
        Old(_temp.CreateFile("a.tmp", 10));

        var items = await ScanAsync(Rule() with { Paths = [_temp.RootPath, _temp.RootPath + @"\"] });

        Assert.Single(items);
    }

    [Fact]
    public async Task ScanAsync_SkipsATempFolderThatIsAJunction()
    {
        var documents = new TestDirectory();
        try
        {
            Old(documents.CreateFile("notes.tmp", 10));
            _temp.CreateJunction("Temp", documents.RootPath);

            Assert.Empty(await ScanAsync(Rule() with { Paths = [Path.Combine(_temp.RootPath, "Temp")] }));
        }
        finally
        {
            documents.Dispose();
        }
    }

    private static string Old(string path)
    {
        File.SetLastWriteTimeUtc(path, OldDate);
        return path;
    }

    private Task<IReadOnlyList<ScanItem>> ScanAsync(CleaningRule? rule = null) =>
        new TempScanner([rule ?? Rule()], new ReparsePointDetector(), TimeProvider.System, NullLogger<TempScanner>.Instance)
            .ScanAsync(Path.GetPathRoot(_temp.RootPath)!, null, CancellationToken.None);

    private CleaningRule Rule() =>
        new("USER_TEMP", "Temp", "Leftovers.", CleaningCategory.Temporary, RiskLevel.Safe, [_temp.RootPath], 2, true);
}
