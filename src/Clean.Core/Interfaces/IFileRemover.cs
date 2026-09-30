using Clean.Core.Models;

namespace Clean.Core.Interfaces;

// KeptCopyPath: for a duplicate, the copy that stays. The removal is refused if it has disappeared,
// so the last copy of a file can never be removed.
public sealed record RemovalRequest(string Path, long SizeBytes, DateTime LastWriteUtc, string? KeptCopyPath = null);

public interface IFileRemover
{
    Task<CleaningResult> RemoveAsync(
        IReadOnlyList<RemovalRequest> requests,
        string ruleId,
        string label,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken);
}
