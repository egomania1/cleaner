using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Infrastructure.Rules;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Rules;

public sealed class JsonRuleLoaderTests : IDisposable
{
    private const string ValidRule = """
        {
          "id": "TEST_CACHE",
          "name": "Cache de test",
          "description": "Se reconstruit tout seul.",
          "category": "gpuCache",
          "risk": "safe",
          "paths": ["%LOCALAPPDATA%\\TestCache"],
          "minimumAgeDays": 3,
          "automaticCleaningAllowed": true
        }
        """;

    private readonly TestDirectory _folder = new();

    private readonly JsonRuleLoader _loader = new(NullLogger<JsonRuleLoader>.Instance);

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Load_ReadsAValidRule()
    {
        Write("rules.json", Json(ValidRule));

        var result = Load();

        Assert.Empty(result.Errors);
        var rule = Assert.Single(result.Rules);
        Assert.Equal("TEST_CACHE", rule.Id);
        Assert.Equal("Cache de test", rule.Name);
        Assert.Equal(CleaningCategory.GpuCache, rule.Category);
        Assert.Equal(RiskLevel.Safe, rule.Risk);
        Assert.Equal([@"%LOCALAPPDATA%\TestCache"], rule.Paths);
        Assert.Equal(3, rule.MinimumAgeDays);
        Assert.True(rule.AutomaticCleaningAllowed);
    }

    [Fact]
    public void Load_SkipsInvalidRulesButKeepsTheOthers()
    {
        var unsafeRule = ValidRule.Replace("TEST_CACHE", "WHOLE_PROFILE").Replace(@"%LOCALAPPDATA%\\TestCache", "%USERPROFILE%");
        Write("rules.json", Json(ValidRule, unsafeRule));

        var result = Load();

        Assert.Equal("TEST_CACHE", Assert.Single(result.Rules).Id);
        Assert.Contains("WHOLE_PROFILE", Assert.Single(result.Errors));
    }

    [Fact]
    public void Load_RejectsDuplicateIdsAcrossFiles()
    {
        Write("a.json", Json(ValidRule));
        Write("b.json", Json(ValidRule));

        var result = Load();

        Assert.Single(result.Rules);
        Assert.Contains("already used", Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "version": 2, "rules": [] }""")]
    [InlineData("""{ "version": 1, "rules": [], "extra": true }""")]
    public void Load_ReportsBrokenFiles(string content)
    {
        Write("broken.json", content);

        var result = Load();

        Assert.Empty(result.Rules);
        Assert.Contains("broken.json", Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData("\"risk\": \"safe\"", "\"risk\": \"yolo\"")]
    [InlineData("\"risk\": \"safe\"", "\"risk\": 0")]
    [InlineData("\"minimumAgeDays\": 3,", "")]
    public void Load_ReportsRulesWithBadOrMissingFields(string original, string replacement)
    {
        Write("rules.json", Json(ValidRule.Replace(original, replacement)));

        var result = Load();

        Assert.Empty(result.Rules);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void Load_ReportsAMissingFolder()
    {
        var result = _loader.Load(Path.Combine(_folder.RootPath, "absent"));

        Assert.Empty(result.Rules);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void ShippedRules_AreAllValid()
    {
        var result = _loader.Load(JsonRuleLoader.DefaultFolder);

        Assert.True(result.Errors.Count == 0, string.Join(Environment.NewLine, result.Errors));
        Assert.True(result.Rules.Count >= 12);
    }

    private RuleLoadResult Load() =>
        _loader.Load(_folder.RootPath, path => path
            .Replace("%LOCALAPPDATA%", @"C:\Users\Test\AppData\Local")
            .Replace("%USERPROFILE%", @"C:\Users\Test"));

    private void Write(string name, string content) =>
        File.WriteAllText(Path.Combine(_folder.RootPath, name), content);

    private static string Json(params string[] rules) => $$"""{ "version": 1, "rules": [{{string.Join(",", rules)}}] }""";
}
