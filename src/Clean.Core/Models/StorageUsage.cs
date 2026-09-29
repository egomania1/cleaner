namespace Clean.Core.Models;

public sealed record StorageUsage(string Label, long SizeBytes, string? Path = null, bool IsDirectory = false);
