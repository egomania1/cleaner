using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Safety;

public sealed class SafetyEngine(IPathValidator pathValidator, IRuleEngine ruleEngine) : ISafetyEngine
{
    public CleaningDecision Evaluate(ScanItem item)
    {
        if (item.Risk == RiskLevel.Blocked)
        {
            return CleaningDecision.Deny(item, "Cet emplacement est bloqué.");
        }

        if (!item.CanClean)
        {
            return CleaningDecision.Deny(item, "Rien à supprimer ici.");
        }

        var validation = pathValidator.Validate(item.Path);
        if (!validation.IsValid)
        {
            return CleaningDecision.Deny(item, validation.Reason);
        }

        // The item must still match the rule it was scanned with, in case the rule files changed since.
        var rule = ruleEngine.FindRuleFor(item.Path);
        if (rule is null || rule.Id != item.RuleId)
        {
            return CleaningDecision.Deny(item, "La règle de cet emplacement a changé, relance l'analyse.");
        }

        if (rule.Risk == RiskLevel.Blocked || rule.Risk > item.Risk)
        {
            return CleaningDecision.Deny(item, "Le niveau de risque de cette règle a changé, relance l'analyse.");
        }

        return CleaningDecision.Allow(item, rule.Description);
    }
}
