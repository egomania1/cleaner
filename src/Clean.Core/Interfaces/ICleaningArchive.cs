using Clean.Core.Models;

namespace Clean.Core.Interfaces;

// Cleaned files are moved here instead of being deleted, so that a cleaning can be undone for a while.
public interface ICleaningArchive
{
    event Action? Changed;

    string CreateSessionId();

    string GetLocationFolder(string sessionId, int locationIndex);

    Task RecordAsync(CleaningSession session, CancellationToken cancellationToken);

    Task<IReadOnlyList<CleaningSession>> GetSessionsAsync(CancellationToken cancellationToken);

    Task<RestoreResult> RestoreAsync(string sessionId, CancellationToken cancellationToken);

    Task<long> FreeAsync(string sessionId, CancellationToken cancellationToken);

    Task<long> FreeExpiredAsync(CancellationToken cancellationToken);
}
