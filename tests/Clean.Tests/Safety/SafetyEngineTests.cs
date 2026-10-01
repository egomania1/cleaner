using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;

namespace Clean.Tests.Safety;

public class SafetyEngineTests
{
    private const string CacheFolder = @"C:\Users\Test\AppData\Local\NVIDIA\DXCache";

    private static readonly RuleEngine Engine = new(
        [new CleaningRule("NVIDIA_DX", "Cache", "Rebuilt.", CleaningCategory.GpuCache, RiskLevel.Safe, [@"%LOCALAPPDATA%\NVIDIA\DXCache"], 0, true)],
        Expand);

    private static readonly SafetyEngine Safety = new(new PathValidator(Engine, new NoLinks(), Expand), Engine);

    [Fact]
    public void Evaluate_AllowsAnItemThatMatchesItsRuleFolder()
    {
        Assert.True(Safety.Evaluate(Item(CacheFolder)).IsAllowed);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Users\Test\AppData\Local")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\DXCache\..\..")]
    [InlineData(@"C:\Users\Test\Documents")]
    [InlineData("relative\\folder")]
    public void Evaluate_DeniesAnythingButTheExactRuleFolder(string path)
    {
        Assert.False(Safety.Evaluate(Item(path)).IsAllowed);
    }

    [Fact]
    public void Evaluate_DeniesAnItemScannedWithAnotherRule()
    {
        Assert.False(Safety.Evaluate(Item(CacheFolder) with { RuleId = "OTHER" }).IsAllowed);
    }

    [Fact]
    public void Evaluate_DeniesWhenTheRuleBecameRiskier()
    {
        var riskier = new RuleEngine(
            [new CleaningRule("NVIDIA_DX", "Cache", "Rebuilt.", CleaningCategory.GpuCache, RiskLevel.Expert, [@"%LOCALAPPDATA%\NVIDIA\DXCache"], 0, false)],
            Expand);
        var safety = new SafetyEngine(new PathValidator(riskier, new NoLinks(), Expand), riskier);

        Assert.False(safety.Evaluate(Item(CacheFolder)).IsAllowed);
    }

    [Fact]
    public void Evaluate_DeniesItemsWithNothingToClean()
    {
        Assert.False(Safety.Evaluate(Item(CacheFolder) with { CanClean = false }).IsAllowed);
    }

    private static string Expand(string path) => path.Replace("%LOCALAPPDATA%", @"C:\Users\Test\AppData\Local");

    private static ScanItem Item(string path) =>
        TestItems.Create() with { Path = path, RuleId = "NVIDIA_DX" };
}
