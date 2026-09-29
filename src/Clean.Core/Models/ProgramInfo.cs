namespace Clean.Core.Models;

public sealed record ProgramInfo(
    string Name,
    string? Publisher,
    string? Version,
    DateOnly? InstalledOn,
    string? Location);
