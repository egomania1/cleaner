namespace Clean.Core.Startup;

public enum StartupSource
{
    UserRegistry,
    MachineRegistry,
    MachineRegistry32,
    UserFolder,
    CommonFolder,
}

// Id is stable between two reads, so a screen can ask for a change by id and nothing else: a name typed
// by a user, or sent from anywhere, can never address a registry value that was not listed.
public sealed record StartupEntry(
    string Id,
    string Name,
    string Command,
    string? ExecutablePath,
    StartupSource Source,
    bool IsEnabled,
    string? Description,
    string? Publisher,
    bool TargetMissing)
{
    // Entries of all users need administrator rights to change, and are left to Task Manager.
    public bool CanChange => Source is StartupSource.UserRegistry or StartupSource.UserFolder;

    public bool IsFolder => Source is StartupSource.UserFolder or StartupSource.CommonFolder;
}

public enum StartupChangeResult
{
    Changed,
    NotFound,
    NeedsAdministrator,
    Failed,
}
