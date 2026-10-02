using Clean.Core.Interfaces;
using Clean.Core.Maintenance;
using Clean.Core.Settings;

namespace Clean.Infrastructure.Settings;

public sealed class SettingsStore(string path) : ISettingsStore
{
    public static readonly string DefaultPath = JsonFile.InAppFolder("settings.json");

    private AppSettings? _current;

    public AppSettings Current => _current ??= JsonFile.Read<AppSettings>(path) ?? new AppSettings();

    public void Save(AppSettings settings)
    {
        _current = settings;
        JsonFile.Write(path, settings);
    }
}

public sealed class MaintenanceReportStore(string path) : IMaintenanceReportStore
{
    public static readonly string DefaultPath = JsonFile.InAppFolder("maintenance-report.json");

    // Read every time: the report is written by the silent run, a different process from the window.
    public MaintenanceReport? Last => JsonFile.Read<MaintenanceReport>(path);

    public void Save(MaintenanceReport report) => JsonFile.Write(path, report);
}
