using System.Globalization;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Clean.Infrastructure.Windows;

// Reads the same list as Settings › Apps: the "Uninstall" registry keys of the machine and of the current user.
public sealed class InstalledProgramCatalog(ILogger<InstalledProgramCatalog> logger) : IInstalledProgramCatalog
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly Lazy<IReadOnlyList<ProgramInfo>> _programs = new(() => ReadAll(logger));

    public IReadOnlyList<ProgramInfo> ProgramsAt(string path) => ProgramMatcher.ProgramsAt(_programs.Value, path);

    private static IReadOnlyList<ProgramInfo> ReadAll(ILogger logger)
    {
        var programs = new List<ProgramInfo>();
        ReadKey(Registry.LocalMachine, UninstallKey, programs, logger);
        ReadKey(Registry.LocalMachine, UninstallKey32, programs, logger);
        ReadKey(Registry.CurrentUser, UninstallKey, programs, logger);
        return programs;
    }

    private static void ReadKey(RegistryKey hive, string keyPath, List<ProgramInfo> programs, ILogger logger)
    {
        try
        {
            using var uninstall = hive.OpenSubKey(keyPath);
            if (uninstall is null)
            {
                return;
            }

            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var entry = uninstall.OpenSubKey(name);
                if (entry is not null && ToProgram(entry) is { } program)
                {
                    programs.Add(program);
                }
            }
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            logger.LogWarning(exception, "Could not read installed programs from {Key}", keyPath);
        }
    }

    private static ProgramInfo? ToProgram(RegistryKey entry)
    {
        var name = entry.GetValue("DisplayName") as string;
        if (string.IsNullOrWhiteSpace(name) || entry.GetValue("SystemComponent") is 1)
        {
            return null;
        }

        return new ProgramInfo(
            Name: name.Trim(),
            Publisher: entry.GetValue("Publisher") as string,
            Version: entry.GetValue("DisplayVersion") as string,
            InstalledOn: ParseInstallDate(entry.GetValue("InstallDate") as string),
            Location: LocationOf(entry));
    }

    private static string? LocationOf(RegistryKey entry)
    {
        if (entry.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
        {
            return location;
        }

        // Many installers leave InstallLocation empty but point DisplayIcon at their main executable ("path,index").
        if (entry.GetValue("DisplayIcon") is string icon && !string.IsNullOrWhiteSpace(icon))
        {
            var iconPath = icon.Split(',')[0].Trim().Trim('"');
            return Path.GetDirectoryName(iconPath);
        }

        return null;
    }

    private static DateOnly? ParseInstallDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}
