using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Safety;

// Deny by default: only the exact folders named by a loaded rule can be cleaned, and only when
// nothing on the way to them is protected or a link that could lead somewhere else.
public sealed class PathValidator(
    IRuleEngine ruleEngine,
    IReparsePointDetector reparsePointDetector,
    Func<string, string>? expand = null) : IPathValidator
{
    private readonly Func<string, string> _expand = expand ?? Environment.ExpandEnvironmentVariables;
    private readonly ProtectedPathService _protectedPaths = new(expand);

    public PathValidationResult Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return PathValidationResult.Rejected("Le chemin n'est pas un chemin complet.");
        }

        // Network shares and \\?\ device paths are never cleaning targets and skip Windows' own path checks.
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.Contains('/'))
        {
            return PathValidationResult.Rejected("Seuls les dossiers d'un disque local peuvent être nettoyés.");
        }

        if (HasTraversal(path))
        {
            return PathValidationResult.Rejected("Le chemin contient « . » ou « .. » et pourrait sortir du dossier prévu.");
        }

        // A colon after the drive letter would name an alternate data stream, not a folder.
        if (path.IndexOf(':', 2) >= 0)
        {
            return PathValidationResult.Rejected("Le chemin contient un caractère interdit.");
        }

        var target = ProtectedPathService.Normalize(path);
        if (Path.GetPathRoot(target) is { } root && string.Equals(root, target, StringComparison.OrdinalIgnoreCase))
        {
            return PathValidationResult.Rejected("La racine d'un disque ne peut jamais être nettoyée.");
        }

        if (_protectedPaths.FindProtectedFolderWithin(target) is { } protectedFolder)
        {
            return PathValidationResult.Rejected($"Ce chemin contient le dossier protégé {protectedFolder}.");
        }

        if (!IsRuleFolder(target))
        {
            return PathValidationResult.Rejected("Aucune règle ne désigne ce dossier.");
        }

        if (reparsePointDetector.FindLinkOnPath(target) is { } link)
        {
            return PathValidationResult.Rejected($"{link} est un lien vers un autre emplacement, il n'est jamais suivi.");
        }

        return PathValidationResult.Valid(target);
    }

    private bool IsRuleFolder(string target)
    {
        foreach (var rule in ruleEngine.Rules)
        {
            foreach (var rawPath in rule.Paths)
            {
                var expanded = _expand(rawPath);
                if (!expanded.Contains('%') && Path.IsPathFullyQualified(expanded)
                    && string.Equals(ProtectedPathService.Normalize(expanded), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasTraversal(string path) =>
        path.Split('\\').Any(segment => segment is "." or "..");
}
