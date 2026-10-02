using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Maintenance;

// What a pass nobody watches may touch: items of a rule that is both marked safe and explicitly allowed to
// run automatically, that the scan found cleanable, and that the safety engine still allows.
public static class MaintenancePolicy
{
    public static IReadOnlyList<CleaningDecision> Select(IEnumerable<ScanItem> items, IRuleEngine rules, ISafetyEngine safety)
    {
        var automatic = rules.Rules
            .Where(rule => rule.AutomaticCleaningAllowed && rule.Risk == RiskLevel.Safe)
            .Select(rule => rule.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return items
            .Where(item => item.CanClean && item.Risk == RiskLevel.Safe && !item.RequiresConfirmation && automatic.Contains(item.RuleId))
            .Select(safety.Evaluate)
            .Where(decision => decision.IsAllowed)
            .ToList();
    }
}
