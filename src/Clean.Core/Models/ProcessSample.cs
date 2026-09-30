namespace Clean.Core.Models;

// ExecutablePath and ProcessorTime are null for processes the current user is not allowed to inspect.
public sealed record ProcessSample(
    int Id,
    DateTime? StartTime,
    string Name,
    string? ExecutablePath,
    string? Description,
    TimeSpan? ProcessorTime,
    long MemoryBytes);
