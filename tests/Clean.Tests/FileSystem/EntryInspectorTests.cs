using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.FileSystem;

public sealed class EntryInspectorTests : IDisposable
{
    private readonly TestDirectory _folder = new();
    private readonly FakeProgramCatalog _catalog = new();
    private readonly EntryInspector _inspector;

    public EntryInspectorTests()
    {
        _inspector = new EntryInspector(_catalog, NullLogger<EntryInspector>.Instance);
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task InspectAsync_CountsFilesFoldersAndChildren()
    {
        _folder.CreateFile(@"Videos\clip.mp4", 300);
        _folder.CreateFile(@"Videos\Old\old.mp4", 100);
        _folder.CreateFile("notes.txt", 10);

        var details = await _inspector.InspectAsync(_folder.RootPath, isDirectory: true, CancellationToken.None);

        Assert.Equal(3, details.FileCount);
        Assert.Equal(2, details.FolderCount);
        Assert.Equal(400, Assert.Single(details.Children, child => child.Label == "Videos").SizeBytes);
        Assert.Equal(400, Assert.Single(details.Composition, usage => usage.Extension == ".mp4").SizeBytes);
    }

    [Fact]
    public async Task InspectAsync_ReportsTheMostRecentModification()
    {
        var older = _folder.CreateFile("old.txt", 10);
        var newer = _folder.CreateFile(@"Sub\new.txt", 10);
        File.SetLastWriteTime(older, new DateTime(2020, 1, 1));
        File.SetLastWriteTime(newer, new DateTime(2025, 6, 15));

        var details = await _inspector.InspectAsync(_folder.RootPath, isDirectory: true, CancellationToken.None);

        Assert.Equal(new DateTime(2025, 6, 15), details.LastModified!.Value.DateTime);
    }

    [Fact]
    public async Task InspectAsync_UsesTheInstalledProgramCatalog()
    {
        _folder.CreateFile("app.bin", 10);
        _catalog.Programs = [new ProgramInfo("Test App", "Test Publisher", "1.0", null, _folder.RootPath)];

        var details = await _inspector.InspectAsync(_folder.RootPath, isDirectory: true, CancellationToken.None);

        Assert.Equal("Test App", Assert.Single(details.Programs).Name);
    }

    [Fact]
    public async Task InspectAsync_NeverFollowsJunctions()
    {
        var outside = new TestDirectory();
        try
        {
            outside.CreateFile("huge.bin", 5_000);
            _folder.CreateFile(@"Cache\cache.bin", 20);
            _folder.CreateJunction(@"Cache\Link", outside.RootPath);

            var details = await _inspector.InspectAsync(_folder.RootPath, isDirectory: true, CancellationToken.None);

            Assert.Equal(1, details.FileCount);
            Assert.Equal(20, Assert.Single(details.Children).SizeBytes);
        }
        finally
        {
            outside.Dispose();
        }
    }

    [Fact]
    public async Task InspectAsync_File()
    {
        var path = _folder.CreateFile("setup.iso", 50);

        var details = await _inspector.InspectAsync(path, isDirectory: false, CancellationToken.None);

        Assert.False(details.IsDirectory);
        Assert.Equal(".iso", Assert.Single(details.Composition).Extension);
        Assert.Empty(details.Children);
    }

    private sealed class FakeProgramCatalog : IInstalledProgramCatalog
    {
        public IReadOnlyList<ProgramInfo> Programs { get; set; } = [];

        public IReadOnlyList<ProgramInfo> ProgramsAt(string path) =>
            Programs.Where(program => string.Equals(program.Location, path, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
