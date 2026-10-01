using System.Text.Json;
using System.Text.Json.Serialization;
using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Cleaning;

// Layout: <root>\history.json lists the sessions, <root>\sessions\<id>\<location index>\ holds the files
// with their paths relative to the cleaned folder, which is all a restore needs.
public sealed class CleaningArchive(
    string rootFolder,
    TimeProvider clock,
    ILogger<CleaningArchive> logger,
    IReparsePointDetector? reparsePointDetector = null) : ICleaningArchive
{
    public static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clean", "Archive");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly EnumerationOptions RecursiveOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IReparsePointDetector _links = reparsePointDetector ?? new ReparsePointDetector();

    public event Action? Changed;

    private string HistoryPath => Path.Combine(rootFolder, "history.json");

    private string SessionsFolder => Path.Combine(rootFolder, "sessions");

    public string CreateSessionId() => $"{clock.GetUtcNow():yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

    // Files are archived on their own drive: moving them to another one would copy them, slowly,
    // and fill the other drive instead of freeing anything.
    public string GetLocationFolder(string sessionId, int locationIndex, string? locationPath = null) =>
        Path.Combine(SessionFolder(sessionId, locationPath), locationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public async Task RecordAsync(CleaningSession session, CancellationToken cancellationToken)
    {
        await UpdateHistoryAsync(sessions => [.. sessions.Where(existing => existing.Id != session.Id), session], cancellationToken);
        Changed?.Invoke();
    }

    public async Task<IReadOnlyList<CleaningSession>> GetSessionsAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return (await ReadHistoryAsync(cancellationToken)).OrderByDescending(session => session.CleanedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<RestoreResult> RestoreAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await FindAsync(sessionId, cancellationToken);
        if (session is not { CanRestore: true })
        {
            return new RestoreResult(0, 0, 0, 0);
        }

        var result = await Task.Run(() => MoveBack(session, cancellationToken), CancellationToken.None);

        // A file that could not be moved back stays in the archive, so the session can be restored again later.
        if (result.FailedFileCount == 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Run(() => DeleteSessionFolders(session), CancellationToken.None);
            await SetStatusAsync(sessionId, CleaningSessionStatus.Restored);
        }

        Changed?.Invoke();
        return result;
    }

    public async Task<long> FreeAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await FindAsync(sessionId, cancellationToken);
        if (session is not { CanRestore: true })
        {
            return 0;
        }

        var freed = await Task.Run(() => DeleteSessionFolders(session), CancellationToken.None);
        await SetStatusAsync(sessionId, CleaningSessionStatus.Freed);
        Changed?.Invoke();
        return freed;
    }

    public async Task<long> FreeExpiredAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        long freed = 0;

        foreach (var session in await GetSessionsAsync(cancellationToken))
        {
            if (!session.CanRestore || session.ExpiresAt > now)
            {
                continue;
            }

            // One session that cannot be freed must not stop the others from giving their space back.
            try
            {
                freed += await FreeAsync(session.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not free the archived session {Session}", session.Id);
            }
        }

        return freed;
    }

    private RestoreResult MoveBack(CleaningSession session, CancellationToken cancellationToken)
    {
        long restored = 0, restoredBytes = 0, conflicts = 0, failed = 0;

        foreach (var location in session.Locations)
        {
            var source = GetLocationFolder(session.Id, location.Index, location.Path);
            if (!Directory.Exists(source))
            {
                continue;
            }

            var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location.Path));
            var files = new DirectoryInfo(source).EnumerateFiles("*", RecursiveOptions).ToList();

            // The folder may have become a link since the cleaning: the files would land somewhere else.
            // They stay in the archive, so the session can still be restored once the link is gone.
            if (_links.FindLinkOnPath(target) is { } link)
            {
                logger.LogWarning("Not restoring into {Folder}: {Link} is a link to another location", target, link);
                failed += files.Count;
                continue;
            }

            foreach (var file in files)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new RestoreResult(restored, restoredBytes, conflicts, failed);
                }

                var destination = Path.GetFullPath(Path.Combine(target, Path.GetRelativePath(source, file.FullName)));
                if (!destination.StartsWith(target + '\\', StringComparison.OrdinalIgnoreCase))
                {
                    failed++;
                    continue;
                }

                // The application recreated this file since the cleaning; its current version wins.
                if (File.Exists(destination))
                {
                    conflicts++;
                    continue;
                }

                try
                {
                    var size = file.Length;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    file.MoveTo(destination);
                    restored++;
                    restoredBytes += size;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(exception, "Could not restore {File}", destination);
                    failed++;
                }
            }
        }

        return new RestoreResult(restored, restoredBytes, conflicts, failed);
    }

    private long DeleteSessionFolders(CleaningSession session)
    {
        var folders = session.Locations
            .Select(location => SessionFolder(session.Id, location.Path))
            .Append(SessionFolder(session.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return folders.Sum(DeleteFolder);
    }

    private long DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        long freed = 0;
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", RecursiveOptions).ToList())
        {
            try
            {
                var size = file.Length;
                file.Attributes = FileAttributes.Normal;
                file.Delete();
                freed += size;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not delete the archived file {File}", file.FullName);
            }
        }

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not remove the archive folder {Folder}", folder);
        }

        return freed;
    }

    private string SessionFolder(string sessionId, string? locationPath = null)
    {
        // Ids come from our own history file, but a hand-edited one must not point outside the archive.
        if (sessionId.Length == 0 || sessionId.IndexOfAny(['\\', '/', ':', '.']) >= 0)
        {
            throw new ArgumentException($"'{sessionId}' is not a valid session id.", nameof(sessionId));
        }

        var archiveDrive = Path.GetPathRoot(Path.GetFullPath(rootFolder));
        var locationDrive = locationPath is null ? null : Path.GetPathRoot(Path.GetFullPath(locationPath));
        if (locationDrive is null || string.Equals(locationDrive, archiveDrive, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(SessionsFolder, sessionId);
        }

        var volumeArchive = Path.Combine(locationDrive, UserFileScope.VolumeArchiveFolderName);
        if (!Directory.Exists(volumeArchive))
        {
            Directory.CreateDirectory(volumeArchive).Attributes |= FileAttributes.Hidden;
        }

        return Path.Combine(volumeArchive, "sessions", sessionId);
    }

    private async Task<CleaningSession?> FindAsync(string sessionId, CancellationToken cancellationToken) =>
        (await GetSessionsAsync(cancellationToken)).FirstOrDefault(session => session.Id == sessionId);

    private Task SetStatusAsync(string sessionId, CleaningSessionStatus status) =>
        UpdateHistoryAsync(
            sessions => sessions
                .Select(session => session.Id == sessionId ? session with { Status = status, ClosedAt = clock.GetUtcNow() } : session)
                .ToList(),
            CancellationToken.None);

    private async Task UpdateHistoryAsync(Func<IReadOnlyList<CleaningSession>, IReadOnlyList<CleaningSession>> update, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sessions = update(await ReadHistoryAsync(cancellationToken));
            Directory.CreateDirectory(rootFolder);

            // Written next to the real file first, so a crash mid-write never leaves a half history.
            var temporary = HistoryPath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(sessions, JsonOptions), cancellationToken);
            File.Move(temporary, HistoryPath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<CleaningSession>> ReadHistoryAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(HistoryPath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(HistoryPath);
            var sessions = await JsonSerializer.DeserializeAsync<List<CleaningSession?>>(stream, JsonOptions, cancellationToken) ?? [];

            // A hand-edited or damaged entry is left out, never allowed to break everything else.
            var valid = sessions.OfType<CleaningSession>().Where(IsWellFormed).ToList();
            if (valid.Count != sessions.Count)
            {
                logger.LogError("{Count} unusable sessions were left out of the cleaning history", sessions.Count - valid.Count);
                File.Copy(HistoryPath, HistoryPath + ".broken", overwrite: true);
            }

            return valid;
        }
        catch (JsonException exception)
        {
            // Kept aside, because the next write would otherwise replace it with an empty history.
            logger.LogError(exception, "The cleaning history is unreadable");
            File.Copy(HistoryPath, HistoryPath + ".broken", overwrite: true);
            return [];
        }
    }

    private static bool IsWellFormed(CleaningSession session) =>
        !string.IsNullOrEmpty(session.Id)
        && session.Id.IndexOfAny(['\\', '/', ':', '.']) < 0
        && session.Locations is not null
        && session.Locations.All(location =>
            location is not null && !string.IsNullOrWhiteSpace(location.Path) && Path.IsPathFullyQualified(location.Path));
}
