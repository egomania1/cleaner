using Clean.Core.Models;

namespace Clean.Core.Safety;

// SAFE: a cache or temp folder that is clearly identified and rebuilt by its app.
// CAUTION: removable, but the app may rebuild slowly or lose non-critical state.
// EXPERT: only on a manual choice, after a warning.
// BLOCKED: Clean refuses; nothing can turn it into an allowed operation.
public static class RiskAnalyzer
{
    public static RiskAssessment Analyze(CleaningRule? rule, PathValidationResult validation)
    {
        if (!validation.IsValid)
        {
            return new RiskAssessment(RiskLevel.Blocked, validation.Reason);
        }

        if (rule is null)
        {
            return new RiskAssessment(RiskLevel.Blocked, "Aucune règle ne décrit cet emplacement.");
        }

        return new RiskAssessment(rule.Risk, rule.Description);
    }
}
