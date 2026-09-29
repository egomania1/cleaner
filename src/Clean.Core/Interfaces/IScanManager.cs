using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IScanManager
{
    IReadOnlyList<IScanner> Scanners { get; }

    Task<ScanResult> RunAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
