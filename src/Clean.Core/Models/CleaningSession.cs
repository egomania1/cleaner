namespace Clean.Core.Models;

public sealed record CleaningSession(
    string Id,
    DateTimeOffset CleanedAt,
    DateTimeOffset ExpiresAt,
    string DriveName,
    long SizeBytes,
    long FileCount,
    IReadOnlyList<CleaningSessionLocation> Locations,
    CleaningSessionStatus Status,
    DateTimeOffset? ClosedAt)
{
    // Long enough to notice that something is missing, short enough for the space to come back soon.
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    public bool CanRestore => Status == CleaningSessionStatus.Restorable;
}
