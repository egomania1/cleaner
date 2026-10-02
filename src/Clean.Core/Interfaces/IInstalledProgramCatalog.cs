using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IInstalledProgramCatalog
{
    IReadOnlyList<ProgramInfo> All { get; }

    IReadOnlyList<ProgramInfo> ProgramsAt(string path);

    // Reads the registry again, e.g. after an uninstall.
    void Reload();
}
