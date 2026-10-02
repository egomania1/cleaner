using Clean.Core.Maintenance;
using Clean.Core.Settings;

namespace Clean.Core.Interfaces;

public interface ISettingsStore
{
    AppSettings Current { get; }

    void Save(AppSettings settings);
}

public interface IMaintenanceReportStore
{
    MaintenanceReport? Last { get; }

    void Save(MaintenanceReport report);
}

public enum ScheduleResult
{
    Done,
    Failed,
}

// The Windows scheduled task that starts Clean in silent maintenance mode. It runs as the current user,
// only while they are signed in, and needs no administrator rights.
public interface IMaintenanceScheduler
{
    bool IsScheduled();

    ScheduleResult Enable();

    ScheduleResult Disable();
}

public enum RestorePointOutcome
{
    Created,
    Declined,
    Failed,
}

public interface IRestorePointService
{
    Task<RestorePointOutcome> CreateAsync(string description, CancellationToken cancellationToken);
}
