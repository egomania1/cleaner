using Clean.Core.Models;
using Clean.Core.Safety;

namespace Clean.Tests.Safety;

public class RiskAnalyzerTests
{
    private static readonly CleaningRule Rule =
        new("NVIDIA_DX", "Cache", "Rebuilt.", CleaningCategory.GpuCache, RiskLevel.Caution, [@"%LOCALAPPDATA%\NVIDIA\DXCache"], 0, false);

    [Fact]
    public void Analyze_UsesTheRuleRiskForAValidPath()
    {
        var assessment = RiskAnalyzer.Analyze(Rule, PathValidationResult.Valid(@"C:\Cache"));

        Assert.Equal(RiskLevel.Caution, assessment.Risk);
        Assert.Equal(Rule.Description, assessment.Reason);
    }

    [Fact]
    public void Analyze_BlocksAnInvalidPathWithItsReason()
    {
        var assessment = RiskAnalyzer.Analyze(Rule, PathValidationResult.Rejected("Lien."));

        Assert.Equal(RiskLevel.Blocked, assessment.Risk);
        Assert.Equal("Lien.", assessment.Reason);
    }

    [Fact]
    public void Analyze_BlocksAPathWithoutRule()
    {
        Assert.Equal(RiskLevel.Blocked, RiskAnalyzer.Analyze(null, PathValidationResult.Valid(@"C:\Cache")).Risk);
    }
}
