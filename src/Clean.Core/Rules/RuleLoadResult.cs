using Clean.Core.Models;

namespace Clean.Core.Rules;

public sealed record RuleLoadResult(IReadOnlyList<CleaningRule> Rules, IReadOnlyList<string> Errors);
