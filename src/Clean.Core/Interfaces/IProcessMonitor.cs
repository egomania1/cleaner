using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IProcessMonitor
{
    int ProcessorCount { get; }

    long TotalMemoryBytes { get; }

    IReadOnlyList<ProcessSample> Sample();
}
