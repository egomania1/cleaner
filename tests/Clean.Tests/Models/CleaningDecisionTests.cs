using Clean.Core.Models;

namespace Clean.Tests.Models;

public class CleaningDecisionTests
{
    [Fact]
    public void Allow_ReturnsAllowedDecision_ForSafeItem()
    {
        var decision = CleaningDecision.Allow(TestItems.Create(risk: RiskLevel.Safe), "Known cache");

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Allow_Throws_ForBlockedItem()
    {
        var blockedItem = TestItems.Create(risk: RiskLevel.Blocked);

        Assert.Throws<InvalidOperationException>(() => CleaningDecision.Allow(blockedItem, "Should fail"));
    }

    [Fact]
    public void Deny_ReturnsRefusedDecision()
    {
        var decision = CleaningDecision.Deny(TestItems.Create(), "Protected folder");

        Assert.False(decision.IsAllowed);
        Assert.Equal("Protected folder", decision.Reason);
    }
}
