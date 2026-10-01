namespace Clean.Core.Models;

public sealed record CleaningRule(
    string Id,
    string Name,
    string Description,
    CleaningCategory Category,
    RiskLevel Risk,
    IReadOnlyList<string> Paths,
    int MinimumAgeDays,
    bool AutomaticCleaningAllowed,
    string? ProcessName = null);
