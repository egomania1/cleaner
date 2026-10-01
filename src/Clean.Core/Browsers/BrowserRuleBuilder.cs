using System.Text.RegularExpressions;
using Clean.Core.Models;
using Clean.Core.Rules;

namespace Clean.Core.Browsers;

// Turns detected cache folders into ordinary cleaning rules, so they go through the same validation,
// scan and safety chain as every other rule. Only cache folders are ever described: user data has no rule.
public static partial class BrowserRuleBuilder
{
    private const string Assurance = "Tes mots de passe, cookies, sessions, favoris, extensions et profils ne sont pas touchés. Ferme le navigateur pour libérer tout l'espace.";

    public static RuleLoadResult Build(IEnumerable<BrowserCacheLocation> locations, Func<string, string> expand)
    {
        var rules = new List<CleaningRule>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            var rule = ToRule(location, ids);
            var problems = RuleValidator.Validate(rule, expand);
            if (problems.Count == 0)
            {
                rules.Add(rule);
            }
            else
            {
                errors.Add($"{rule.Id}: {string.Join("; ", problems)}");
            }
        }

        return new RuleLoadResult(rules, errors);
    }

    private static CleaningRule ToRule(BrowserCacheLocation location, HashSet<string> ids)
    {
        var baseId = Sanitize($"{location.BrowserId}_{location.ProfileName}_{location.FolderName}");
        var id = baseId;
        for (var suffix = 2; !ids.Add(id); suffix++)
        {
            id = $"{baseId}_{suffix}";
        }

        var profile = location.ProfileName is null ? string.Empty : $" ({location.ProfileName})";
        return new CleaningRule(
            Id: id,
            Name: $"{KindLabel(location.Kind)} de {location.BrowserName}{profile}",
            Description: $"{KindDescription(location.Kind)} {Assurance}",
            Category: location.Kind == BrowserCacheKind.Gpu || location.Kind == BrowserCacheKind.Shader
                ? CleaningCategory.GpuCache
                : CleaningCategory.BrowserCache,
            Risk: RiskLevel.Safe,
            Paths: [location.Path],
            MinimumAgeDays: 0,
            AutomaticCleaningAllowed: true,
            ProcessName: location.ProcessName);
    }

    private static string KindLabel(BrowserCacheKind kind) => kind switch
    {
        BrowserCacheKind.Http => "Cache web",
        BrowserCacheKind.Code => "Cache de code",
        BrowserCacheKind.Gpu => "Cache graphique",
        _ => "Cache de shaders",
    };

    private static string KindDescription(BrowserCacheKind kind) => kind switch
    {
        BrowserCacheKind.Http => "Copies des pages, images et fichiers des sites déjà visités, gardées pour les afficher plus vite. Elles se retéléchargent au besoin.",
        BrowserCacheKind.Code => "Code des sites web déjà préparé par le navigateur pour démarrer plus vite. Il se reconstruit tout seul.",
        BrowserCacheKind.Gpu => "Données d'affichage gardées par le navigateur. Elles se recréent à la prochaine utilisation.",
        _ => "Programmes graphiques compilés par le navigateur. Ils se recréent, les premières pages un peu chargées peuvent mettre un instant à s'afficher.",
    };

    private static string Sanitize(string value) =>
        NonIdCharacters().Replace(value.ToUpperInvariant(), "_").Trim('_');

    [GeneratedRegex("[^A-Z0-9]+")]
    private static partial Regex NonIdCharacters();
}
