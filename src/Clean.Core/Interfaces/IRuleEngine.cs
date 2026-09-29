using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IRuleEngine
{
    IReadOnlyList<CleaningRule> Rules { get; }

    CleaningRule? FindRuleFor(string path);
}
