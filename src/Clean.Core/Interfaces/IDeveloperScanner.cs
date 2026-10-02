using Clean.Core.Developer;
using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IDeveloperScanner
{
    Task<DeveloperScanResult> ScanAsync(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
