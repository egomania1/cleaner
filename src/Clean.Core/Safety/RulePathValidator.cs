using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Safety;

// Only the exact folders named by a loaded rule can be cleaned, never a parent, a sibling or a drive root.
public sealed class RulePathValidator(IRuleEngine ruleEngine, Func<string, string>? expand = null) : IPathValidator
{
    private readonly Func<string, string> _expand = expand ?? Environment.ExpandEnvironmentVariables;

    public PathValidationResult Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return PathValidationResult.Rejected("Le chemin n'est pas un chemin complet.");
        }

        var target = Normalize(path);
        if (Path.GetPathRoot(target) is { } root && string.Equals(Path.TrimEndingDirectorySeparator(root), target, StringComparison.OrdinalIgnoreCase))
        {
            return PathValidationResult.Rejected("La racine d'un disque ne peut jamais être nettoyée.");
        }

        foreach (var rule in ruleEngine.Rules)
        {
            foreach (var rawPath in rule.Paths)
            {
                var expanded = _expand(rawPath);
                if (!expanded.Contains('%') && Path.IsPathFullyQualified(expanded)
                    && string.Equals(Normalize(expanded), target, StringComparison.OrdinalIgnoreCase))
                {
                    return PathValidationResult.Valid(target);
                }
            }
        }

        return PathValidationResult.Rejected("Aucune règle ne désigne ce dossier.");
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
