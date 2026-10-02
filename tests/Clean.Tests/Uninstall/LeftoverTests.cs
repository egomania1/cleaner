using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Safety;
using Clean.Infrastructure.Cleaning;
using Clean.Infrastructure.FileSystem;
using Clean.Infrastructure.Uninstall;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Uninstall;

public sealed class LeftoverTests : IDisposable
{
    private readonly TestDirectory _roots = new();
    private readonly TestDirectory _archiveRoot = new();
    private readonly CleaningArchive _archive;
    private readonly FakeCatalog _catalog = new();
    private readonly ProgramInfo _program = new("Foo Studio", "Foo Inc", "1.0", null, null);

    public LeftoverTests()
    {
        _archive = new CleaningArchive(_archiveRoot.RootPath, TimeProvider.System, NullLogger<CleaningArchive>.Instance);
    }

    public void Dispose()
    {
        _roots.Dispose();
        _archiveRoot.Dispose();
    }

    [Fact]
    public async Task FindAsync_ListsFoldersNamedAfterTheProgramInTheKnownRoots()
    {
        _roots.CreateFile(@"ProgramFiles\Foo Studio\data.bin", 3_000);
        _roots.CreateFile(@"ProgramFiles\Foo Studio\sub\more.bin", 2_000);
        _roots.CreateFile(@"ProgramFiles\Other\keep.bin", 9_000);

        var search = await Finder().FindAsync(_program, CancellationToken.None);

        Assert.False(search.StillInstalled);
        var folder = Assert.Single(search.Folders);
        Assert.EndsWith(@"Foo Studio", folder.Path);
        Assert.Equal(5_000, folder.SizeBytes);
        Assert.Equal(2, folder.FileCount);
    }

    [Fact]
    public async Task FindAsync_ReportsAProgramThatIsStillInstalledAndOffersNothing()
    {
        _roots.CreateFile(@"ProgramFiles\Foo Studio\data.bin", 3_000);
        _catalog.Programs = [_program];

        var search = await Finder().FindAsync(_program, CancellationToken.None);

        Assert.True(search.StillInstalled);
        Assert.Empty(search.Folders);
    }

    [Fact]
    public async Task FindAsync_NeverOffersAProtectedFolder()
    {
        _roots.CreateFile(@"ProgramFiles\Foo Studio\data.bin", 3_000);
        var protectedPaths = new ProtectedPathService(_ => Path.Combine(_roots.RootPath, "ProgramFiles", "Foo Studio"));

        var search = await Finder(protectedPaths).FindAsync(_program, CancellationToken.None);

        Assert.Empty(search.Folders);
    }

    [Fact]
    public async Task FindAsync_IgnoresEmptyFoldersAndLinks()
    {
        _roots.CreateDirectory(@"ProgramFiles\Foo Studio");
        using var elsewhere = new TestDirectory();
        elsewhere.CreateFile("data.bin", 1_000);
        _roots.CreateDirectory("AppData");
        _roots.CreateJunction(@"AppData\Foo Studio", elsewhere.RootPath);

        Assert.Empty((await Finder().FindAsync(_program, CancellationToken.None)).Folders);
    }

    [Fact]
    public async Task RemoveAsync_MovesTheChosenFolderToTheArchiveAndItCanBeRestored()
    {
        var file = _roots.CreateFile(@"ProgramFiles\Foo Studio\data.bin", 3_000);
        var folder = Path.GetDirectoryName(file)!;

        var result = await Remover().RemoveAsync(_program, [folder], null, CancellationToken.None);

        Assert.Equal(3_000, result.RemovedBytes);
        Assert.False(Directory.Exists(folder));
        var session = Assert.Single(await _archive.GetSessionsAsync(CancellationToken.None));
        Assert.Equal("Restes de Foo Studio", Assert.Single(session.Locations).Name);

        await _archive.RestoreAsync(session.Id, CancellationToken.None);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task RemoveAsync_RefusesAFolderTheFinderDoesNotOffer()
    {
        var other = _roots.CreateFile(@"ProgramFiles\Documents\thesis.docx", 3_000);

        var result = await Remover().RemoveAsync(_program, [Path.GetDirectoryName(other)!], null, CancellationToken.None);

        Assert.Equal(0, result.RemovedFileCount);
        Assert.True(File.Exists(other));
    }

    [Fact]
    public async Task RemoveAsync_DoesNothingWhileTheProgramIsStillInstalled()
    {
        var file = _roots.CreateFile(@"ProgramFiles\Foo Studio\data.bin", 3_000);
        _catalog.Programs = [_program];

        var result = await Remover().RemoveAsync(_program, [Path.GetDirectoryName(file)!], null, CancellationToken.None);

        Assert.Equal(0, result.RemovedFileCount);
        Assert.True(File.Exists(file));
    }

    private LeftoverFinder Finder(ProtectedPathService? protectedPaths = null) => new(
        _catalog,
        new ReparsePointDetector(),
        protectedPaths ?? new ProtectedPathService(_ => "%unresolved%"),
        [Path.Combine(_roots.RootPath, "ProgramFiles"), Path.Combine(_roots.RootPath, "AppData")],
        NullLogger<LeftoverFinder>.Instance);

    private LeftoverRemover Remover() => new(Finder(), _archive, TimeProvider.System, NullLogger<LeftoverRemover>.Instance);

    private sealed class FakeCatalog : IInstalledProgramCatalog
    {
        public IReadOnlyList<ProgramInfo> Programs { get; set; } = [];

        public IReadOnlyList<ProgramInfo> All => Programs;

        public IReadOnlyList<ProgramInfo> ProgramsAt(string path) => [];

        public void Reload()
        {
        }
    }
}
