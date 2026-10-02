using Clean.Core.Models;
using Clean.Core.Uninstall;

namespace Clean.Core.Interfaces;

public enum UninstallOutcome
{
    // The uninstaller ran and finished (it may still have been cancelled by the user: the registry tells).
    Finished,
    NoCommand,
    CouldNotStart,
}

public interface IProgramUninstaller
{
    Task<UninstallOutcome> RunAsync(ProgramInfo program, CancellationToken cancellationToken);
}

public interface ILeftoverFinder
{
    Task<LeftoverSearch> FindAsync(ProgramInfo program, CancellationToken cancellationToken);
}

public interface ILeftoverRemover
{
    Task<CleaningResult> RemoveAsync(
        ProgramInfo program,
        IReadOnlyList<string> folders,
        IProgress<CleaningProgress>? progress,
        CancellationToken cancellationToken);
}
