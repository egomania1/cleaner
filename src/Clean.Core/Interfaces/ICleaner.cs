using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface ICleaner
{
    Task<CleaningResult> CleanAsync(
        IReadOnlyList<CleaningDecision> decisions,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken);
}
