namespace Clean.Core.Settings;

// RestorePointBeforeCleaning: ask Windows for a restore point before each cleaning (needs administrator rights).
// AutoMaintenance: a weekly silent pass that only touches items marked safe. Both are off until the user turns them on.
public sealed record AppSettings(bool RestorePointBeforeCleaning = false, bool AutoMaintenance = false);
