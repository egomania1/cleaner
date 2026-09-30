using Clean.Core.Models;

namespace Clean.Core.Apps;

public sealed record RunningAppUsage(
    string Key,
    string DisplayName,
    ProgramInfo? Program,
    string? ExecutablePath,
    double CpuPercent,
    long MemoryBytes,
    int ProcessCount);
