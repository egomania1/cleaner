using Clean.Core.Models;
using Clean.Core.Rules;

namespace Clean.Tests.Rules;

public class RuleValidatorTests
{
    private static readonly Dictionary<string, string> Variables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SystemDrive"] = "C:",
        ["WINDIR"] = @"C:\Windows",
        ["USERPROFILE"] = @"C:\Users\Test",
        ["LOCALAPPDATA"] = @"C:\Users\Test\AppData\Local",
        ["APPDATA"] = @"C:\Users\Test\AppData\Roaming",
        ["TEMP"] = @"C:\Users\Test\AppData\Local\Temp",
    };

    [Fact]
    public void Validate_AcceptsAValidRule()
    {
        Assert.Empty(Validate(Rule()));
    }

    [Theory]
    [InlineData("lower_case")]
    [InlineData("")]
    [InlineData("WITH SPACE")]
    public void Validate_RejectsBadIds(string id)
    {
        Assert.Contains(Validate(Rule() with { Id = id }), problem => problem.Contains("id"));
    }

    [Fact]
    public void Validate_RejectsMissingTexts()
    {
        var problems = Validate(Rule() with { Name = " ", Description = "" });

        Assert.Contains(problems, problem => problem.Contains("name"));
        Assert.Contains(problems, problem => problem.Contains("description"));
    }

    [Fact]
    public void Validate_RejectsBlockedRisk()
    {
        Assert.NotEmpty(Validate(Rule() with { Risk = RiskLevel.Blocked, AutomaticCleaningAllowed = false }));
    }

    [Fact]
    public void Validate_RejectsAutomaticCleaningForRiskyRules()
    {
        Assert.Contains(
            Validate(Rule() with { Risk = RiskLevel.Caution, AutomaticCleaningAllowed = true }),
            problem => problem.Contains("automatic"));
    }

    [Fact]
    public void Validate_RejectsNegativeAge()
    {
        Assert.NotEmpty(Validate(Rule() with { MinimumAgeDays = -1 }));
    }

    [Fact]
    public void Validate_RejectsEmptyPaths()
    {
        Assert.NotEmpty(Validate(Rule() with { Paths = [] }));
    }

    [Theory]
    [InlineData(@"C:\Temp")]
    [InlineData(@"%LOCALAPPDATA%\..\..\Documents")]
    [InlineData(@"%LOCALAPPDATA%\Temp\*")]
    [InlineData(@"\\server\share")]
    [InlineData(@"%LOCALAPPDATA%\Temp\")]
    public void Validate_RejectsPathsThatAreNotPlainVariableFolders(string path)
    {
        Assert.NotEmpty(Validate(Rule() with { Paths = [path] }));
    }

    [Theory]
    [InlineData("%USERPROFILE%")]
    [InlineData("%LOCALAPPDATA%")]
    [InlineData("%WINDIR%")]
    [InlineData(@"%USERPROFILE%\Documents")]
    [InlineData(@"%WINDIR%\System32")]
    [InlineData("%SystemDrive%")]
    public void Validate_RejectsPathsThatCoverAProtectedFolder(string path)
    {
        Assert.Contains(Validate(Rule() with { Paths = [path] }), problem => problem.Contains("protected"));
    }

    [Fact]
    public void Validate_IgnoresVariablesMissingOnThisPc()
    {
        Assert.Empty(Validate(Rule() with { Paths = [@"%UNKNOWN_VARIABLE%\Cache"] }));
    }

    private static IReadOnlyList<string> Validate(CleaningRule rule) => RuleValidator.Validate(rule, Expand);

    private static string Expand(string path)
    {
        foreach (var (name, value) in Variables)
        {
            path = path.Replace($"%{name}%", value, StringComparison.OrdinalIgnoreCase);
        }

        return path;
    }

    private static CleaningRule Rule() => new(
        "TEST_RULE",
        "Test",
        "A test rule.",
        CleaningCategory.Temporary,
        RiskLevel.Safe,
        ["%TEMP%", @"%WINDIR%\Temp"],
        MinimumAgeDays: 2,
        AutomaticCleaningAllowed: true);
}
