using Clean.Core.Models;
using Clean.Core.Rules;

namespace Clean.Tests.Rules;

public class BuiltInRulesTests
{
    [Fact]
    public void Rules_HaveUniqueIds()
    {
        var ids = BuiltInRules.All.Select(rule => rule.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Rules_OnlyUseEnvironmentVariablePaths()
    {
        Assert.All(BuiltInRules.All.SelectMany(rule => rule.Paths), path => Assert.StartsWith("%", path));
    }

    [Fact]
    public void Rules_NeverTargetBlockedItems()
    {
        Assert.DoesNotContain(BuiltInRules.All, rule => rule.Risk == RiskLevel.Blocked);
    }

    [Fact]
    public void Rules_OnlyAllowAutomaticCleaningForSafeItems()
    {
        Assert.All(
            BuiltInRules.All.Where(rule => rule.AutomaticCleaningAllowed),
            rule => Assert.Equal(RiskLevel.Safe, rule.Risk));
    }
}
