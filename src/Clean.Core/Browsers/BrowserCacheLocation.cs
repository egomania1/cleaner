namespace Clean.Core.Browsers;

// One cache folder of one browser profile. Path starts with an environment variable, like a rule path.
public sealed record BrowserCacheLocation(
    string BrowserId,
    string BrowserName,
    string? ProfileName,
    string FolderName,
    BrowserCacheKind Kind,
    string Path,
    string ProcessName);
