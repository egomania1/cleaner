namespace Clean.Core.Models;

// EstimatedSizeBytes is what the installer declared to Windows; it can be missing or stale.
public sealed record ProgramInfo(
    string Name,
    string? Publisher,
    string? Version,
    DateOnly? InstalledOn,
    string? Location,
    long? EstimatedSizeBytes = null);
