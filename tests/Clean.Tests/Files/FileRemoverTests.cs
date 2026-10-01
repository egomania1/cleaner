using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Infrastructure.Cleaning;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Files;

public sealed class FileRemoverTests : IDisposable
{
    private readonly TestDirectory _files = new();
    private readonly TestDirectory _archiveRoot = new();
    private readonly CleaningArchive _archive;
    private readonly FakeCatalog _catalog = new();

    public FileRemoverTests()
    {
        _archive = new CleaningArchive(_archiveRoot.RootPath, TimeProvider.System, NullLogger<CleaningArchive>.Instance);
    }

    public void Dispose()
    {
        _files.Dispose();
        _archiveRoot.Dispose();
    }

    [Fact]
    public async Task RemoveAsync_MovesTheFileToTheArchiveAndCanRestoreIt()
    {
        var file = _files.CreateFile(@"Downloads\setup.iso", 1_000);

        var result = await RemoveAsync(Request(file));

        Assert.False(File.Exists(file));
        Assert.Equal(1_000, result.RemovedBytes);
        var session = Assert.Single(await _archive.GetSessionsAsync(CancellationToken.None));
        Assert.Equal("Gros fichiers", Assert.Single(session.Locations).Name);

        await _archive.RestoreAsync(session.Id, CancellationToken.None);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task RemoveAsync_KeepsAFileChangedSinceTheScan()
    {
        var file = _files.CreateFile("video.mp4", 1_000);
        var request = Request(file);
        File.WriteAllBytes(file, new byte[2_000]);

        var result = await RemoveAsync(request);

        Assert.True(File.Exists(file));
        Assert.Equal(0, result.RemovedFileCount);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task RemoveAsync_NeverRemovesTheLastCopyOfADuplicate()
    {
        var first = _files.CreateFile(@"a\photo.jpg", 500);
        var second = _files.CreateFile(@"b\photo.jpg", 500);

        // Both copies asked for removal, each "keeping" the other: neither may go.
        await RemoveAsync(Request(first) with { KeptCopyPath = second }, Request(second) with { KeptCopyPath = first });

        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public async Task RemoveAsync_RemovesACopyWhenTheKeptOneIsThere()
    {
        var first = _files.CreateFile(@"a\photo.jpg", 500);
        var second = _files.CreateFile(@"b\photo.jpg", 500);

        await RemoveAsync(Request(second) with { KeptCopyPath = first });

        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public async Task RemoveAsync_RefusesFilesOfAnInstalledApplication()
    {
        var file = _files.CreateFile(@"Games\Big Game\data.pak", 1_000);
        _catalog.Programs = [new ProgramInfo("Big Game", "Studio", "1", null, Path.Combine(_files.RootPath, "Games", "Big Game"))];

        await RemoveAsync(Request(file));

        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task RemoveAsync_RefusesFilesOutsideTheUserScope()
    {
        var file = _files.CreateFile(@"Protected\important.bin", 1_000);

        await RemoveAsync(Request(file));

        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task RemoveAsync_RefusesAFileReachedThroughAFolderThatBecameAJunction()
    {
        using var elsewhere = new TestDirectory();
        var target = elsewhere.CreateFile("photo.raw", 1_000);
        _files.CreateJunction("Downloads", elsewhere.RootPath);

        var result = await RemoveAsync(Request(Path.Combine(_files.RootPath, "Downloads", "photo.raw")));

        Assert.True(File.Exists(target));
        Assert.Equal(0, result.RemovedFileCount);
    }

    private Task<CleaningResult> RemoveAsync(params RemovalRequest[] requests)
    {
        var scope = new UserFileScope([Path.Combine(_files.RootPath, "Protected"), _archiveRoot.RootPath]);
        var remover = new FileRemover(scope, _catalog, new ReparsePointDetector(), _archive, TimeProvider.System, NullLogger<FileRemover>.Instance);
        return remover.RemoveAsync(requests, "LARGE_FILES", "Gros fichiers", null, CancellationToken.None);
    }

    private static RemovalRequest Request(string path) =>
        new(path, new FileInfo(path).Length, File.GetLastWriteTimeUtc(path));

    private sealed class FakeCatalog : IInstalledProgramCatalog
    {
        public IReadOnlyList<ProgramInfo> Programs { get; set; } = [];

        public IReadOnlyList<ProgramInfo> All => Programs;

        public IReadOnlyList<ProgramInfo> ProgramsAt(string path) => [];
    }
}
