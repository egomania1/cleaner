using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Rules;

public sealed class RuleEngine : IRuleEngine
{
    private readonly Func<string, string> _expand;

    public RuleEngine(IReadOnlyList<CleaningRule> rules, Func<string, string>? expand = null)
    {
        Rules = rules;
        _expand = expand ?? Environment.ExpandEnvironmentVariables;
    }

    public IReadOnlyList<CleaningRule> Rules { get; }

    // When rule folders are nested, the deepest one describes the path best.
    public CleaningRule? FindRuleFor(string path)
    {
        var target = Normalize(path);
        CleaningRule? best = null;
        var bestLength = -1;

        foreach (var rule in Rules)
        {
            foreach (var rawPath in rule.Paths)
            {
                var expanded = _expand(rawPath);
                if (expanded.Contains('%'))
                {
                    continue;
                }

                var folder = Normalize(expanded);
                if (folder.Length > bestLength && IsSameOrInside(target, folder))
                {
                    best = rule;
                    bestLength = folder.Length;
                }
            }
        }

        return best;
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + '\\', StringComparison.OrdinalIgnoreCase);
}
