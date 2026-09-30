using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IScanManager
{
    IReadOnlyList<IScanner> Scanners { get; }

    Task<ScanResult> RunAsync(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
