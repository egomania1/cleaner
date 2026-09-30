using Clean.Core.Files;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Files;

public sealed class FileScannerTests : IDisposable
{
    private readonly TestDirectory _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task ScanAsync_ListsFilesAboveTheMinimumAndSkipsExcludedFolders()
    {
        _folder.CreateFile(@"Videos\film.mkv", 5_000);
        _folder.CreateFile(@"Videos\small.txt", 10);
        _folder.CreateFile(@"Protected\big.bin", 9_000);
        _folder.CreateFile(@"$Recycle.Bin\deleted.bin", 9_000);

        var result = await ScanAsync(minimumSize: 1_000);

        var file = Assert.Single(result.Files);
        Assert.EndsWith("film.mkv", file.Path);
        Assert.Equal(2, result.FilesExamined);
    }

    [Fact]
    public async Task ScanAsync_NeverFollowsJunctions()
    {
        var outside = new TestDirectory();
        try
        {
            outside.CreateFile("secret.bin", 5_000);
            _folder.CreateDirectory("Links");
            _folder.CreateJunction(@"Links\Outside", outside.RootPath);

            Assert.Empty((await ScanAsync(minimumSize: 1)).Files);
        }
        finally
        {
            outside.Dispose();
        }
    }

    // The test folder plays the role of a drive root; its direct files would be skipped as "root files",
    // so everything is created one level down.
    private Task<Clean.Core.Models.FileScanResult> ScanAsync(long minimumSize)
    {
        var scope = new UserFileScope([Path.Combine(_folder.RootPath, "Protected")]);
        var scanner = new FileScanner(scope, NullLogger<FileScanner>.Instance);
        return scanner.ScanAsync(_folder.RootPath, minimumSize, null, CancellationToken.None);
    }
}
