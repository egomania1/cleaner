using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IStorageAnalyzer
{
    Task<IReadOnlyList<StorageUsage>> AnalyzeAsync(
        string rootPath,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
