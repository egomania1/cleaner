using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Clean.Infrastructure.Windows;

// Where a startup program is declared. A registry location has a Hive and a KeyPath, a folder location a FolderPath.
public sealed record StartupLocation(StartupSource Source, RegistryKey? Hive, string? KeyPath, string? FolderPath)
{
    // Windows keeps the "turned off" mark of an entry here, one group per kind of location (what Task Manager reads).
    public string ApprovalGroup => Source switch
    {
        StartupSource.MachineRegistry32 => "Run32",
        StartupSource.UserFolder or StartupSource.CommonFolder => "StartupFolder",
        _ => "Run",
    };
}

// Reads the programs that start with Windows and turns them on or off the way Task Manager does: with the
// StartupApproved mark, never by deleting the entry. Only the current user's own entries can be changed.
public sealed class StartupCatalog(
    IReadOnlyList<StartupLocation> locations,
    RegistryKey userHive,
    RegistryKey? machineHive,
    string approvalBasePath,
    ILogger<StartupCatalog> logger) : IStartupCatalog
{
    public const string DefaultApprovalBasePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKey32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

    public static StartupCatalog ForThisPc(ILogger<StartupCatalog> logger) => new(
        [
            new StartupLocation(StartupSource.UserRegistry, Registry.CurrentUser, RunKey, null),
            new StartupLocation(StartupSource.MachineRegistry, Registry.LocalMachine, RunKey, null),
            new StartupLocation(StartupSource.MachineRegistry32, Registry.LocalMachine, RunKey32, null),
            new StartupLocation(StartupSource.UserFolder, null, null, Environment.GetFolderPath(Environment.SpecialFolder.Startup)),
            new StartupLocation(StartupSource.CommonFolder, null, null, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)),
        ],
        Registry.CurrentUser,
        Registry.LocalMachine,
        DefaultApprovalBasePath,
        logger);

    public IReadOnlyList<StartupEntry> Read()
    {
        var entries = new List<StartupEntry>();
        foreach (var location in locations)
        {
            try
            {
                if (location.Hive is not null && location.KeyPath is not null)
                {
                    ReadRegistry(location, entries);
                }
                else if (location.FolderPath is not null)
                {
                    ReadFolder(location, entries);
                }
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                logger.LogWarning(exception, "Could not read startup entries from {Source}", location.Source);
            }
        }

        return entries
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Source)
            .ToList();
    }

    public StartupChangeResult SetEnabled(string entryId, bool enabled)
    {
        var entry = Read().FirstOrDefault(candidate => candidate.Id == entryId);
        if (entry is null)
        {
            return StartupChangeResult.NotFound;
        }

        if (!entry.CanChange)
        {
            return StartupChangeResult.NeedsAdministrator;
        }

        var group = locations.First(location => location.Source == entry.Source).ApprovalGroup;
        try
        {
            using var key = userHive.CreateSubKey($@"{approvalBasePath}\{group}", writable: true);
            key.SetValue(entry.Name, ApprovalValue(enabled), RegistryValueKind.Binary);
            logger.LogInformation("Startup entry {Source} was turned {State}", entry.Source, enabled ? "on" : "off");
            return StartupChangeResult.Changed;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            logger.LogWarning(exception, "Could not change the startup entry {Source}", entry.Source);
            return StartupChangeResult.Failed;
        }
    }

    // 12 bytes: the state (2 = on, 3 = off), then the time of the change for an entry turned off.
    private static byte[] ApprovalValue(bool enabled)
    {
        var value = new byte[12];
        if (enabled)
        {
            value[0] = 2;
        }
        else
        {
            value[0] = 3;
            BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(value, 4);
        }

        return value;
    }

    private void ReadRegistry(StartupLocation location, List<StartupEntry> entries)
    {
        using var key = location.Hive!.OpenSubKey(location.KeyPath!);
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetValueNames().Where(name => name.Length > 0))
        {
            if (key.GetValue(name) is string command && !string.IsNullOrWhiteSpace(command))
            {
                entries.Add(Build(location, name, command, StartupCommand.ExtractExecutable(command)));
            }
        }
    }

    private void ReadFolder(StartupLocation location, List<StartupEntry> entries)
    {
        if (!Directory.Exists(location.FolderPath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(location.FolderPath))
        {
            var name = Path.GetFileName(file);
            if (!name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            {
                // A shortcut hides its target; the shortcut itself is shown and judged as it is.
                entries.Add(Build(location, name, file, file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file : null));
            }
        }
    }

    private StartupEntry Build(StartupLocation location, string name, string command, string? executable)
    {
        string? description = null;
        string? publisher = null;
        var missing = false;

        if (executable is not null)
        {
            missing = !File.Exists(executable);
            if (!missing)
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(executable);
                    description = Clean(info.FileDescription);
                    publisher = Clean(info.CompanyName);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogDebug(exception, "No version information for a startup program");
                }
            }
        }

        return new StartupEntry(
            $"{location.Source}|{name}",
            name,
            command,
            executable,
            location.Source,
            IsEnabled(location, name),
            description,
            publisher,
            missing);
    }

    // Off when either the user's own mark or the machine's mark says so.
    private bool IsEnabled(StartupLocation location, string name) =>
        !IsMarkedOff(userHive, location.ApprovalGroup, name) && !IsMarkedOff(machineHive, location.ApprovalGroup, name);

    private bool IsMarkedOff(RegistryKey? hive, string group, string name)
    {
        try
        {
            using var key = hive?.OpenSubKey($@"{approvalBasePath}\{group}");
            return key?.GetValue(name) is byte[] { Length: > 0 } value && (value[0] & 1) == 1;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
