namespace Clean.Core.Models;

public sealed record RestoreResult(long RestoredFileCount, long RestoredBytes, long ConflictFileCount, long FailedFileCount);
