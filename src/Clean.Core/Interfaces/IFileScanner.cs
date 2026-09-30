using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(string driveRoot, long minimumSizeBytes, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
