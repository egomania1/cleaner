using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IScanner
{
    string Id { get; }

    string Name { get; }

    Task<IReadOnlyList<ScanItem>> ScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}
