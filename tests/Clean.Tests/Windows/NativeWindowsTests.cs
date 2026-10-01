using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
using Clean.Infrastructure.Windows;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Windows;

public sealed class NativeWindowsTests : IDisposable
{
    private readonly TestDirectory _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task FolderSizer_AddsUpEveryFileInTheFolderAndItsSubfolders()
    {
        _folder.CreateFile("a.bin", 100);
        _folder.CreateFile(@"sub\b.bin", 40);
        _folder.CreateFile(@"sub\deeper\c.bin", 2);

        var size = await Sizer().MeasureAsync(_folder.RootPath, [], CancellationToken.None);

        Assert.Equal(142, size);
    }

    [Fact]
    public async Task FolderSizer_LeavesOutTheExcludedFoldersAndOnlyThem()
    {
        _folder.CreateFile("launcher.exe", 10);
        _folder.CreateFile(@"games\one\data.pak", 1_000);
        _folder.CreateFile(@"games2\data.pak", 5);
        var excluded = Path.Combine(_folder.RootPath, "games");

        var size = await Sizer().MeasureAsync(_folder.RootPath, [excluded], CancellationToken.None);

        Assert.Equal(15, size);
    }

    [Fact]
    public async Task FolderSizer_NeverFollowsAJunctionSoNothingIsCountedTwice()
    {
        using var elsewhere = new TestDirectory();
        elsewhere.CreateFile("huge.bin", 5_000);
        _folder.CreateFile("own.bin", 7);
        _folder.CreateJunction("Linked", elsewhere.RootPath);

        var size = await Sizer().MeasureAsync(_folder.RootPath, [], CancellationToken.None);

        Assert.Equal(7, size);
    }

    [Fact]
    public async Task FolderSizer_ReturnsZeroForAFolderThatDoesNotExist()
    {
        Assert.Equal(0, await Sizer().MeasureAsync(Path.Combine(_folder.RootPath, "absent"), [], CancellationToken.None));
    }

    [Fact]
    public async Task FolderSizer_StopsWhenCancelled()
    {
        _folder.CreateFile("a.bin", 10);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Sizer().MeasureAsync(_folder.RootPath, [], new CancellationToken(canceled: true)));
    }

    [Fact]
    public void ProcessMonitor_SeesTheCurrentProcessWithItsPathAndNeverTheIdleProcess()
    {
        var samples = new ProcessMonitor().Sample();

        Assert.DoesNotContain(samples, sample => sample.Id == 0);
        var current = Assert.Single(samples, sample => sample.Id == Environment.ProcessId);
        Assert.False(string.IsNullOrEmpty(current.ExecutablePath));
        Assert.True(File.Exists(current.ExecutablePath));
        Assert.NotNull(current.ProcessorTime);
        Assert.True(current.MemoryBytes > 0);
    }

    [Fact]
    public void ProcessMonitor_DoesNotCountAProcessWithoutAWindowAsHavingOne()
    {
        // The test host is a console process: nothing the user could see or close.
        var current = new ProcessMonitor().Sample().Single(sample => sample.Id == Environment.ProcessId);

        Assert.False(current.HasWindow);
    }

    [Fact]
    public void ProcessMonitor_GivesTheSameIdentityOnTheSecondSample()
    {
        var monitor = new ProcessMonitor();

        var first = monitor.Sample().Single(sample => sample.Id == Environment.ProcessId);
        var second = monitor.Sample().Single(sample => sample.Id == Environment.ProcessId);

        Assert.Equal(first.StartTime, second.StartTime);
        Assert.Equal(first.ExecutablePath, second.ExecutablePath);
        Assert.True(second.ProcessorTime >= first.ProcessorTime);
    }

    [Fact]
    public void ProcessMonitor_ReportsTheMachineFacts()
    {
        var monitor = new ProcessMonitor();

        Assert.Equal(Environment.ProcessorCount, monitor.ProcessorCount);
        Assert.True(monitor.TotalMemoryBytes > 0);
    }

    [Fact]
    public void InstalledProgramCatalog_ReadsNamedProgramsAndNeverReturnsABlankName()
    {
        var catalog = new InstalledProgramCatalog(NullLogger<InstalledProgramCatalog>.Instance);

        var programs = catalog.All;

        Assert.All(programs, program => Assert.False(string.IsNullOrWhiteSpace(program.Name)));
        Assert.Same(catalog.All, catalog.All);
    }

    [Fact]
    public void InstalledProgramCatalog_ProgramsAtDoesNotThrowForUnknownPaths()
    {
        var catalog = new InstalledProgramCatalog(NullLogger<InstalledProgramCatalog>.Instance);

        Assert.NotNull(catalog.ProgramsAt(Path.Combine(_folder.RootPath, "nothing-installed-here")));
    }

    private static FolderSizer Sizer() => new(NullLogger<FolderSizer>.Instance);
}
