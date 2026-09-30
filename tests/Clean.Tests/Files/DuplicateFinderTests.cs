using System.Diagnostics;
using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Files;

public sealed class DuplicateFinderTests : IDisposable
{
    private readonly TestDirectory _folder = new();
    private readonly DuplicateFinder _finder = new(NullLogger<DuplicateFinder>.Instance);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task FindAsync_GroupsIdenticalFilesWherever_TheyAre()
    {
        var content = Content(300_000, seed: 1);
        var first = Write(@"a\photo.jpg", content);
        var second = Write(@"b\copie de photo.jpg", content);
        Write(@"c\other.jpg", Content(300_000, seed: 2));

        var group = Assert.Single(await FindAsync(first, second, Path.Combine(_folder.RootPath, @"c\other.jpg")));

        Assert.Equal(2, group.Files.Count);
        Assert.Equal(300_000, group.WastedBytes);
    }

    [Fact]
    public async Task FindAsync_TellsApartFilesThatOnlyDifferInTheMiddle()
    {
        var content = Content(1_000_000, seed: 3);
        var changed = (byte[])content.Clone();
        changed[500_000] ^= 0xFF;

        Assert.Empty(await FindAsync(Write("a.bin", content), Write("b.bin", changed)));
    }

    [Fact]
    public async Task FindAsync_ComparesSmallFilesEntirely()
    {
        var content = Content(100_000, seed: 4);
        var changed = (byte[])content.Clone();
        changed[70_000] ^= 0xFF;

        Assert.Empty(await FindAsync(Write("a.bin", content), Write("b.bin", changed)));
    }

    [Fact]
    public async Task FindAsync_IgnoresHardLinksToTheSameFile()
    {
        var original = Write("original.bin", Content(50_000, seed: 5));
        var link = Path.Combine(_folder.RootPath, "link.bin");
        using (var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /H \"{link}\" \"{original}\"") { CreateNoWindow = true, UseShellExecute = false })!)
        {
            process.WaitForExit();
        }

        Assert.True(File.Exists(link));
        Assert.Empty(await FindAsync(original, link));
    }

    [Fact]
    public async Task FindAsync_IgnoresEmptyFiles()
    {
        Assert.Empty(await FindAsync(Write("a.txt", []), Write("b.txt", [])));
    }

    private Task<IReadOnlyList<Clean.Core.Files.DuplicateGroup>> FindAsync(params string[] paths) =>
        _finder.FindAsync(paths.Select(path => new FoundFile(path, new FileInfo(path).Length, File.GetLastWriteTimeUtc(path))).ToList(), null, CancellationToken.None);

    private string Write(string relativePath, byte[] content)
    {
        var path = _folder.CreateFile(relativePath, 0);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] Content(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }
}
