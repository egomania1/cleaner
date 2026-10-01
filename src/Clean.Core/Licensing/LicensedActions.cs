using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Licensing;

// The screens already explain why a button does nothing; these wrappers are the backstop, so that no
// screen, now or later, can remove files without a licence or a running trial.
public sealed class LicensedCleaner(ICleaner inner, ILicenseService license) : ICleaner
{
    public Task<CleaningResult> CleanAsync(
        IReadOnlyList<CleaningDecision> decisions,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken) =>
        license.Access.CanClean
            ? inner.CleanAsync(decisions, progress, cancellationToken)
            : throw new LicenseRequiredException();
}

public sealed class LicensedFileRemover(IFileRemover inner, ILicenseService license) : IFileRemover
{
    public Task<CleaningResult> RemoveAsync(
        IReadOnlyList<RemovalRequest> requests,
        string ruleId,
        string label,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken) =>
        license.Access.CanClean
            ? inner.RemoveAsync(requests, ruleId, label, progress, cancellationToken)
            : throw new LicenseRequiredException();
}
