using Clean.Core.Models;
using Clean.Core.Scanning;

namespace Clean.Tests.Scanning;

public class ScanProgressTrackerTests
{
    [Fact]
    public void AddFile_AccumulatesFilesAndBytes()
    {
        var tracker = new ScanProgressTracker(null, "test");

        tracker.AddFile(@"C:\a.bin", 10);
        tracker.AddFile(@"C:\b.bin", 25);

        Assert.Equal(2, tracker.FilesScanned);
        Assert.Equal(35, tracker.BytesAnalyzed);
    }

    [Fact]
    public void Report_SendsCurrentTotals()
    {
        var progress = new SynchronousProgress<ScanProgress>();
        var tracker = new ScanProgressTracker(progress, "Stockage");
        tracker.AddFile(@"C:\a.bin", 10);

        tracker.Report();

        var last = progress.Reports[^1];
        Assert.Equal("Stockage", last.CurrentScanner);
        Assert.Equal(@"C:\a.bin", last.CurrentPath);
        Assert.Equal(1, last.FilesScanned);
        Assert.Equal(10, last.BytesAnalyzed);
    }
}
