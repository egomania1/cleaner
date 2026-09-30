using Clean.Core.Files;
using Clean.Core.Models;

namespace Clean.Tests.Files;

public class UserFileScopeTests
{
    private static readonly UserFileScope Scope = new([@"C:\Windows", @"C:\Program Files", @"C:\Users\Test\AppData"]);

    [Theory]
    [InlineData(@"C:\Users\Test\Downloads\video.mp4")]
    [InlineData(@"D:\Films\film.mkv")]
    [InlineData(@"C:\Users\Test\Documents\Program Files backup\a.zip")]
    public void Contains_AcceptsUserFiles(string path)
    {
        Assert.True(Scope.Contains(path));
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\big.dll")]
    [InlineData(@"C:\Program Files\App\data.pak")]
    [InlineData(@"C:\Users\Test\AppData\Local\Temp\x.tmp")]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"D:\$Recycle.Bin\S-1-5\file.bin")]
    [InlineData(@"D:\System Volume Information\tracking.log")]
    [InlineData(@"D:\.CleanArchive\sessions\1\0\file.bin")]
    public void Contains_RejectsSystemAndHiddenFolders(string path)
    {
        Assert.False(Scope.Contains(path));
    }

    [Fact]
    public void Keeper_ChoosesByStrategyAndStaysStable()
    {
        var old = new FoundFile(@"C:\B\long\path\a.bin", 10, new DateTime(2020, 1, 1));
        var recent = new FoundFile(@"C:\A\a.bin", 10, new DateTime(2025, 1, 1));
        var group = new DuplicateGroup("id", 10, [old, recent]);

        Assert.Same(old, Keeper.Choose(group, KeepStrategy.Oldest));
        Assert.Same(recent, Keeper.Choose(group, KeepStrategy.Newest));
        Assert.Same(recent, Keeper.Choose(group, KeepStrategy.ShortestPath));
        Assert.Equal(10, group.WastedBytes);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(100, 1)]
    [InlineData(300, 2)]
    [InlineData(500, 3)]
    [InlineData(1000, 4)]
    public void AgeBuckets_SortFilesByLastModification(int daysAgo, int expected)
    {
        var now = new DateTime(2026, 9, 30);

        Assert.Equal(expected, AgeBuckets.IndexOf(now.AddDays(-daysAgo), now));
    }
}
