namespace Clean.Core.Models;

// ExecutablePath and ProcessorTime are null for processes the current user is not allowed to inspect.
// HasWindow means a window the user can see: what tells an application the user launched from a background process.
public sealed record ProcessSample(
    int Id,
    DateTime? StartTime,
    string Name,
    string? ExecutablePath,
    string? Description,
    TimeSpan? ProcessorTime,
    long MemoryBytes,
    bool HasWindow = false);
