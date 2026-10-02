using Clean.Core.Developer;
using Clean.Core.Files;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Developer;

public sealed class DeveloperScannerTests : IDisposable
{
    private readonly TestDirectory _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task ScanAsync_FindsFoldersNextToTheirProjectFileAndMeasuresThem()
    {
        _folder.CreateFile(@"web\package.json", 10);
        _folder.CreateFile(@"web\node_modules\left-pad\index.js", 3_000);
        _folder.CreateFile(@"web\node_modules\left-pad\nested\more.js", 2_000);
        _folder.CreateFile(@"api\Api.csproj", 10);
        _folder.CreateFile(@"api\bin\Debug\api.dll", 4_000);

        var result = await ScanAsync();

        Assert.Equal(2, result.Folders.Count);
        var node = Assert.Single(result.Folders, folder => folder.Kind.Id == "NODE_MODULES");
        Assert.Equal(5_000, node.SizeBytes);
        Assert.Equal(2, node.Files.Count);
        Assert.Equal("web", node.ProjectName);
        Assert.Single(result.Folders, folder => folder.Kind.Id == "DOTNET_BIN");
    }

    [Fact]
    public async Task ScanAsync_IgnoresLookAlikeFoldersWithoutAProjectFile()
    {
        _folder.CreateFile(@"photos\node_modules\a.jpg", 1_000);
        _folder.CreateFile(@"stuff\bin\tool.exe", 1_000);

        Assert.Empty((await ScanAsync()).Folders);
    }

    [Fact]
    public async Task ScanAsync_SkipsExcludedAndHiddenFolders()
    {
        _folder.CreateFile(@"Protected\app\package.json", 10);
        _folder.CreateFile(@"Protected\app\node_modules\x.js", 1_000);
        _folder.CreateFile(@"repo\.git\hooks\package.json", 10);
        _folder.CreateFile(@"repo\.git\hooks\node_modules\y.js", 1_000);
        _folder.CreateFile(@".vscode\extensions\some.extension-1.0\package.json", 10);
        _folder.CreateFile(@".vscode\extensions\some.extension-1.0\node_modules\z.js", 1_000);

        Assert.Empty((await ScanAsync()).Folders);
    }

    [Fact]
    public async Task ScanAsync_StillRecognisesAHiddenBuildFolderNextToItsProject()
    {
        _folder.CreateFile(@"site\package.json", 10);
        _folder.CreateFile(@"site\.next\cache\page.js", 1_000);

        var folder = Assert.Single((await ScanAsync()).Folders);
        Assert.Equal("NEXT_CACHE", folder.Kind.Id);
    }

    [Fact]
    public async Task ScanAsync_NeverFollowsJunctionsInsideAFolder()
    {
        var outside = new TestDirectory();
        try
        {
            outside.CreateFile("secret.bin", 9_000);
            _folder.CreateFile(@"web\package.json", 10);
            _folder.CreateFile(@"web\node_modules\pkg\index.js", 1_000);
            _folder.CreateJunction(@"web\node_modules\linked", outside.RootPath);

            var folder = Assert.Single((await ScanAsync()).Folders);
            Assert.Equal(1_000, folder.SizeBytes);
        }
        finally
        {
            outside.Dispose();
        }
    }

    [Fact]
    public async Task ScanAsync_IgnoresEmptyFolders()
    {
        _folder.CreateFile(@"web\package.json", 10);
        _folder.CreateDirectory(@"web\node_modules");

        Assert.Empty((await ScanAsync()).Folders);
    }

    private Task<DeveloperScanResult> ScanAsync()
    {
        var scope = new UserFileScope([Path.Combine(_folder.RootPath, "Protected")]);
        var scanner = new DeveloperScanner(scope, NullLogger<DeveloperScanner>.Instance);
        return scanner.ScanAsync(_folder.RootPath, null, CancellationToken.None);
    }
}
