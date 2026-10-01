using Clean.Infrastructure.FileSystem;

namespace Clean.Tests.FileSystem;

public sealed class ReparsePointDetectorTests : IDisposable
{
    private readonly TestDirectory _root = new();
    private readonly TestDirectory _documents = new();
    private readonly ReparsePointDetector _detector = new();

    public void Dispose()
    {
        _root.Dispose();
        _documents.Dispose();
    }

    [Fact]
    public void FindLinkOnPath_ReturnsNullForARegularFolder()
    {
        Assert.Null(_detector.FindLinkOnPath(_root.CreateDirectory(@"App\Cache")));
    }

    [Fact]
    public void FindLinkOnPath_FindsTheFolderItself()
    {
        _root.CreateJunction("Cache", _documents.RootPath);
        var link = Path.Combine(_root.RootPath, "Cache");

        Assert.Equal(link, _detector.FindLinkOnPath(link));
    }

    [Fact]
    public void FindLinkOnPath_FindsAJunctionedParent()
    {
        Directory.CreateDirectory(Path.Combine(_documents.RootPath, "Cache"));
        _root.CreateJunction("App", _documents.RootPath);

        Assert.Equal(Path.Combine(_root.RootPath, "App"), _detector.FindLinkOnPath(Path.Combine(_root.RootPath, "App", "Cache")));
    }

    [Fact]
    public void FindLinkOnPath_ReturnsNullForAMissingFolder()
    {
        Assert.Null(_detector.FindLinkOnPath(Path.Combine(_root.RootPath, "Missing", "Cache")));
    }
}
