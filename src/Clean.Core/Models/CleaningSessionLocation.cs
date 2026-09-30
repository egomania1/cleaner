namespace Clean.Core.Models;

// Index names the archive subfolder that holds this location's files, with their paths relative to Path.
public sealed record CleaningSessionLocation(int Index, string RuleId, string Name, string Path, long SizeBytes, long FileCount);
