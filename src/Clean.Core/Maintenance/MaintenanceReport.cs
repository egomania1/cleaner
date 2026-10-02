namespace Clean.Core.Maintenance;

public enum MaintenanceStatus
{
    Done,
    NothingToClean,
    LicenseRequired,
    Failed,
}

public sealed record MaintenanceReport(
    DateTimeOffset RanAt,
    MaintenanceStatus Status,
    long FreedBytes,
    long FileCount,
    long LockedFileCount,
    int LocationCount);
