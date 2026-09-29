using System.Diagnostics;
using Clean.Core.Models;

namespace Clean.Core.Scanning;

public sealed class ScanProgressTracker(IProgress<ScanProgress>? progress, string scannerName)
{
    // Reporting every file would flood the UI thread with hundreds of thousands of updates.
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastReport = TimeSpan.Zero;
    private string _currentPath = string.Empty;

    public long FilesScanned { get; private set; }

    public long BytesAnalyzed { get; private set; }

    public void AddFile(string path, long sizeBytes)
    {
        FilesScanned++;
        BytesAnalyzed += sizeBytes;
        _currentPath = path;

        if (_stopwatch.Elapsed - _lastReport >= ReportInterval)
        {
            Report();
        }
    }

    public void Report()
    {
        _lastReport = _stopwatch.Elapsed;
        progress?.Report(new ScanProgress(
            CurrentScanner: scannerName,
            CurrentPath: _currentPath,
            FilesScanned: FilesScanned,
            BytesAnalyzed: BytesAnalyzed,
            PotentialRecoveryBytes: 0,
            Elapsed: _stopwatch.Elapsed));
    }
}
