using Clean.Core.Browsers;
using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;
using Clean.Infrastructure.Browsers;
using Clean.Tests.Safety;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Browsers;

public sealed class BrowserDetectorTests : IDisposable
{
    private const string ChromeData = @"Local\Google\Chrome\User Data";

    private readonly TestDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Detect_FindsNothingWhenNoBrowserIsInstalled()
    {
        Assert.Empty(Detect());
    }

    [Fact]
    public void Detect_ListsEachProfileCacheAndSharedCache()
    {
        _temp.CreateFile($@"{ChromeData}\Default\Cache\Cache_Data\f_000001", 10);
        _temp.CreateFile($@"{ChromeData}\Default\Code Cache\js\a", 10);
        _temp.CreateFile($@"{ChromeData}\Profile 2\GPUCache\data_0", 10);
        _temp.CreateFile($@"{ChromeData}\ShaderCache\data_1", 10);

        var folders = Detect().Select(location => (location.ProfileName, location.FolderName, location.Kind)).ToList();

        Assert.Equal(
            [
                ("Default", "Cache", BrowserCacheKind.Http),
                ("Default", "Code Cache", BrowserCacheKind.Code),
                ("Profile 2", "GPUCache", BrowserCacheKind.Gpu),
                (null, "ShaderCache", BrowserCacheKind.Shader),
            ],
            folders);
    }

    [Fact]
    public void Detect_UsesProfileNamesFromLocalState()
    {
        _temp.CreateFile($@"{ChromeData}\Default\Cache\x", 1);
        File.WriteAllText(
            Path.Combine(_temp.RootPath, ChromeData, "Local State"),
            """{"profile":{"info_cache":{"Default":{"name":"Travail"}}}}""");

        Assert.Equal("Travail", Assert.Single(Detect()).ProfileName);
    }

    [Fact]
    public void Detect_SurvivesAnUnreadableLocalState()
    {
        _temp.CreateFile($@"{ChromeData}\Default\Cache\x", 1);
        File.WriteAllText(Path.Combine(_temp.RootPath, ChromeData, "Local State"), "not json");

        Assert.Equal("Default", Assert.Single(Detect()).ProfileName);
    }

    [Fact]
    public void Detect_NeverListsUserData()
    {
        foreach (var name in new[] { "Login Data", "Cookies", "Bookmarks", "Web Data", "History" })
        {
            _temp.CreateFile($@"{ChromeData}\Default\{name}", 10);
        }

        _temp.CreateDirectory($@"{ChromeData}\Default\Extensions");
        _temp.CreateDirectory($@"{ChromeData}\Default\Local Storage");
        _temp.CreateDirectory($@"{ChromeData}\Default\Sessions");
        _temp.CreateDirectory($@"{ChromeData}\Default\Network");

        Assert.Empty(Detect());
    }

    [Fact]
    public void Detect_FindsFirefoxProfilesByTheirLocalCache()
    {
        _temp.CreateFile(@"Local\Mozilla\Firefox\Profiles\ab12cd34.default-release\cache2\entries\e1", 10);
        _temp.CreateFile(@"Local\Mozilla\Firefox\Profiles\ab12cd34.default-release\places.sqlite", 10);

        var location = Assert.Single(Detect());

        Assert.Equal("Firefox", location.BrowserName);
        Assert.Equal("default-release", location.ProfileName);
        Assert.Equal("cache2", location.FolderName);
    }

    [Fact]
    public void Detect_KnowsWhichProgramEachCacheBelongsTo()
    {
        _temp.CreateFile($@"{ChromeData}\Default\Cache\x", 1);
        _temp.CreateFile(@"Local\Microsoft\Edge\User Data\Default\Cache\x", 1);
        _temp.CreateFile(@"Local\Mozilla\Firefox\Profiles\ab.p\cache2\x", 1);

        var processes = Detect().Select(location => location.ProcessName).Order().ToList();
        var rules = BrowserRuleBuilder.Build(Detect(), Expand).Rules;

        Assert.Equal(["chrome", "firefox", "msedge"], processes);
        Assert.All(rules, rule => Assert.False(string.IsNullOrEmpty(rule.ProcessName)));
    }

    [Fact]
    public void Detect_FindsOperaCachesDirectlyUnderItsFolder()
    {
        _temp.CreateFile(@"Local\Opera Software\Opera Stable\Cache\Cache_Data\f", 10);

        var location = Assert.Single(Detect());

        Assert.Equal("Opera", location.BrowserName);
        Assert.Null(location.ProfileName);
    }

    [Fact]
    public void Build_TurnsLocationsIntoValidSafeRulesWithUniqueIds()
    {
        _temp.CreateFile($@"{ChromeData}\Default\Cache\x", 1);
        _temp.CreateFile($@"{ChromeData}\Profile 1\Cache\x", 1);
        _temp.CreateFile($@"{ChromeData}\ShaderCache\x", 1);
        _temp.CreateFile(@"Local\Mozilla\Firefox\Profiles\ab12cd34.default-release\cache2\x", 1);
        File.WriteAllText(
            Path.Combine(_temp.RootPath, ChromeData, "Local State"),
            """{"profile":{"info_cache":{"Default":{"name":"Même nom"},"Profile 1":{"name":"Même nom"}}}}""");

        var result = BrowserRuleBuilder.Build(Detect(), Expand);

        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Rules.Count);
        Assert.Equal(4, result.Rules.Select(rule => rule.Id).Distinct().Count());
        Assert.All(result.Rules, rule =>
        {
            Assert.Equal(RiskLevel.Safe, rule.Risk);
            Assert.Contains("mots de passe", rule.Description);
        });
    }

    [Fact]
    public void Build_OnlyEverTargetsCacheFolders()
    {
        _temp.CreateDirectory($@"{ChromeData}\Default\Cache");
        _temp.CreateDirectory($@"{ChromeData}\Default\Login Data");
        _temp.CreateDirectory($@"{ChromeData}\Default\Network");
        _temp.CreateDirectory(@"Local\Mozilla\Firefox\Profiles\ab.p\cache2");
        _temp.CreateDirectory(@"Local\Mozilla\Firefox\Profiles\ab.p\storage");

        var allowed = new[] { "Cache", "Code Cache", "GPUCache", "DawnGraphiteCache", "DawnWebGPUCache", "ShaderCache", "GrShaderCache", "GraphiteDawnCache", "cache2", "startupCache" };
        var rules = BrowserRuleBuilder.Build(Detect(), Expand).Rules;

        Assert.All(rules.SelectMany(rule => rule.Paths), path => Assert.Contains(Path.GetFileName(path), allowed));
    }

    [Fact]
    public void SafetyChain_AcceptsACacheFolderButNeverUserData()
    {
        var profile = Path.Combine(_temp.RootPath, ChromeData, "Default");
        _temp.CreateFile($@"{ChromeData}\Default\Cache\x", 1);
        _temp.CreateFile($@"{ChromeData}\Default\Login Data", 1);

        var rules = BrowserRuleBuilder.Build(Detect(), Expand).Rules;
        var validator = new PathValidator(new RuleEngine(rules, Expand), new NoLinks(), Expand);

        Assert.True(validator.Validate(Path.Combine(profile, "Cache")).IsValid);
        Assert.False(validator.Validate(Path.Combine(profile, "Login Data")).IsValid);
        Assert.False(validator.Validate(profile).IsValid);
        Assert.False(validator.Validate(Path.Combine(_temp.RootPath, ChromeData)).IsValid);
    }

    [Theory]
    [InlineData(@"%LOCALAPPDATA%\Google\Chrome\User Data")]
    [InlineData(@"%LOCALAPPDATA%\Microsoft\Edge\User Data")]
    [InlineData(@"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data")]
    [InlineData(@"%APPDATA%\Opera Software")]
    [InlineData(@"%APPDATA%\Mozilla\Firefox")]
    public void ProtectedPaths_CoverEveryBrowserUserDataFolder(string folder)
    {
        var protectedPaths = new ProtectedPathService(Expand);

        Assert.NotNull(protectedPaths.FindProtectedFolderWithin(Expand(folder)));
    }

    private IReadOnlyList<BrowserCacheLocation> Detect() =>
        new BrowserDetector(NullLogger<BrowserDetector>.Instance, Expand).Detect();

    private string Expand(string path) => path
        .Replace("%LOCALAPPDATA%", Path.Combine(_temp.RootPath, "Local"))
        .Replace("%APPDATA%", Path.Combine(_temp.RootPath, "Roaming"))
        .Replace("%USERPROFILE%", Path.Combine(_temp.RootPath, "User"))
        .Replace("%SystemDrive%", _temp.RootPath[..2]);
}
