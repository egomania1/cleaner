using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface ICleaner
{
    Task<long> CleanAsync(IReadOnlyList<CleaningDecision> decisions, CancellationToken cancellationToken);
}
