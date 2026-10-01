using Clean.Core.Startup;

namespace Clean.Core.Interfaces;

public interface IStartupCatalog
{
    IReadOnlyList<StartupEntry> Read();

    // Turning an entry off keeps it listed, so it can be turned back on: nothing is deleted, as Task Manager does.
    StartupChangeResult SetEnabled(string entryId, bool enabled);
}
