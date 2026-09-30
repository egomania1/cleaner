namespace Clean.Core.Models;

public sealed record CleaningProgress(string CurrentPath, long RemovedFileCount, long RemovedBytes);
