using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Safety;

// A rule only says what could be cleaned; this engine has the final word, just before anything is touched.
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
        var rule = validation.IsValid ? ruleEngine.FindRuleFor(validation.CanonicalPath!) : null;
        var assessment = RiskAnalyzer.Analyze(rule, validation);
        if (assessment.Risk == RiskLevel.Blocked)
        {
            return CleaningDecision.Deny(item, assessment.Reason);
        }

        // The item must still match the rule it was scanned with, in case the rule files changed since.
        if (rule!.Id != item.RuleId)
        {
            return CleaningDecision.Deny(item, "La règle de cet emplacement a changé, relance l'analyse.");
        }

        if (assessment.Risk > item.Risk)
        {
            return CleaningDecision.Deny(item, "Le niveau de risque de cette règle a changé, relance l'analyse.");
        }

        return CleaningDecision.Allow(item, assessment.Reason);
    }
}
