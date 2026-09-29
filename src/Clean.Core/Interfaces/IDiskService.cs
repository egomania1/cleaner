using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IDiskService
{
    Task<IReadOnlyList<DiskInfo>> GetDisksAsync(CancellationToken cancellationToken);
}
