namespace Clean.Core.Models;

public sealed record CleaningResult(
    long RemovedBytes,
    long RemovedFileCount,
    long LockedFileCount,
    long KeptRecentFileCount,
    int CleanedLocationCount,
    bool WasCancelled,
    TimeSpan Duration,
    CleaningSession? Session);
