using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IDiskService
{
    IReadOnlyList<DiskInfo> GetDisks();
}
