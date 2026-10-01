using System.Text.Json;
using System.Text.RegularExpressions;
using Clean.Core.Browsers;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Browsers;

// Looks for installed browsers and their profiles, and lists only their cache folders.
// Passwords, cookies, sessions, bookmarks, extensions and autofill live in other folders that are never listed.
public sealed partial class BrowserDetector(ILogger<BrowserDetector> logger, Func<string, string>? expand = null)
{
    private static readonly (string FolderName, BrowserCacheKind Kind)[] ProfileCaches =
    [
        ("Cache", BrowserCacheKind.Http),
        ("Code Cache", BrowserCacheKind.Code),
        ("GPUCache", BrowserCacheKind.Gpu),
        ("DawnGraphiteCache", BrowserCacheKind.Shader),
        ("DawnWebGPUCache", BrowserCacheKind.Shader),
    ];

    private static readonly (string FolderName, BrowserCacheKind Kind)[] SharedCaches =
    [
        ("ShaderCache", BrowserCacheKind.Shader),
        ("GrShaderCache", BrowserCacheKind.Shader),
        ("GraphiteDawnCache", BrowserCacheKind.Shader),
    ];

    private static readonly (string FolderName, BrowserCacheKind Kind)[] FirefoxCaches =
    [
        ("cache2", BrowserCacheKind.Http),
        ("startupCache", BrowserCacheKind.Code),
    ];

    // ProcessName is the executable name without ".exe": the cache is only cleaned cleanly once it is closed.
    private static readonly (string Id, string Name, string UserData, string ProcessName)[] ChromiumBrowsers =
    [
        ("CHROME", "Chrome", @"%LOCALAPPDATA%\Google\Chrome\User Data", "chrome"),
        ("EDGE", "Edge", @"%LOCALAPPDATA%\Microsoft\Edge\User Data", "msedge"),
        ("BRAVE", "Brave", @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data", "brave"),
    ];

    // Opera keeps its profile in one place and its caches in another; the cache side looks like a user data folder.
    private static readonly (string Id, string Name, string Root, string ProcessName)[] OperaBrowsers =
    [
        ("OPERA", "Opera", @"%LOCALAPPDATA%\Opera Software\Opera Stable", "opera"),
        ("OPERA_GX", "Opera GX", @"%LOCALAPPDATA%\Opera Software\Opera GX Stable", "opera"),
    ];

    private const string FirefoxProfiles = @"%LOCALAPPDATA%\Mozilla\Firefox\Profiles";

    private readonly Func<string, string> _expand = expand ?? Environment.ExpandEnvironmentVariables;

    public IReadOnlyList<BrowserCacheLocation> Detect()
    {
        var locations = new List<BrowserCacheLocation>();

        foreach (var (id, name, userData, processName) in ChromiumBrowsers)
        {
            DetectChromium(id, name, userData, processName, includeRoot: false, locations);
        }

        foreach (var (id, name, root, processName) in OperaBrowsers)
        {
            DetectChromium(id, name, root, processName, includeRoot: true, locations);
        }

        DetectFirefox(locations);
        return locations;
    }

    private void DetectChromium(string id, string name, string userData, string processName, bool includeRoot, List<BrowserCacheLocation> locations)
    {
        if (!Directory.Exists(_expand(userData)))
        {
            return;
        }

        var profileNames = ReadProfileNames(_expand(userData));
        foreach (var folder in ChromiumProfileFolders(_expand(userData)))
        {
            profileNames.TryGetValue(folder, out var displayName);
            AddCaches(locations, id, name, processName, displayName ?? folder, $@"{userData}\{folder}", ProfileCaches);
        }

        if (includeRoot)
        {
            AddCaches(locations, id, name, processName, null, userData, ProfileCaches);
        }

        AddCaches(locations, id, name, processName, null, userData, SharedCaches);
    }

    private void DetectFirefox(List<BrowserCacheLocation> locations)
    {
        var profiles = _expand(FirefoxProfiles);
        if (!Directory.Exists(profiles))
        {
            return;
        }

        foreach (var directory in SafeDirectories(profiles))
        {
            var folder = Path.GetFileName(directory);
            AddCaches(locations, "FIREFOX", "Firefox", "firefox", FirefoxProfileName(folder), $@"{FirefoxProfiles}\{folder}", FirefoxCaches);
        }
    }

    private void AddCaches(
        List<BrowserCacheLocation> locations,
        string id,
        string name,
        string processName,
        string? profileName,
        string parent,
        (string FolderName, BrowserCacheKind Kind)[] caches)
    {
        foreach (var (folderName, kind) in caches)
        {
            var path = $@"{parent}\{folderName}";
            if (Directory.Exists(_expand(path)))
            {
                locations.Add(new BrowserCacheLocation(id, name, profileName, folderName, kind, path, processName));
            }
        }
    }

    private IEnumerable<string> ChromiumProfileFolders(string userData) =>
        SafeDirectories(userData)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(folder => ProfileFolderName().IsMatch(folder))
            .Order(StringComparer.OrdinalIgnoreCase);

    // Only the display name of each profile is read from Local State; nothing else in that file is used.
    private Dictionary<string, string> ReadProfileNames(string userData)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(userData, "Local State")));
            if (document.RootElement.TryGetProperty("profile", out var profile)
                && profile.TryGetProperty("info_cache", out var infoCache))
            {
                foreach (var entry in infoCache.EnumerateObject())
                {
                    if (entry.Value.TryGetProperty("name", out var displayName) && displayName.GetString() is { Length: > 0 } text)
                    {
                        names[entry.Name] = text;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            logger.LogDebug(exception, "No profile names for {Folder}", userData);
        }

        return names;
    }

    private IEnumerable<string> SafeDirectories(string folder)
    {
        try
        {
            return Directory.EnumerateDirectories(folder).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Could not list {Folder}", folder);
            return [];
        }
    }

    // Firefox profile folders look like "x1y2z3ab.default-release": the prefix is random.
    private static string FirefoxProfileName(string folder)
    {
        var dot = folder.IndexOf('.');
        return dot >= 0 && dot < folder.Length - 1 ? folder[(dot + 1)..] : folder;
    }

    [GeneratedRegex(@"^(Default|Profile \d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileFolderName();
}
