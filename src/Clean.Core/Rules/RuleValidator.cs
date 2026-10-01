using System.Text.RegularExpressions;
using Clean.Core.Models;
using Clean.Core.Safety;

namespace Clean.Core.Rules;

// Rules come from editable files, so every rule is checked before it can reach a scanner.
public static partial class RuleValidator
{
    public static IReadOnlyList<string> Validate(CleaningRule rule, Func<string, string> expand)
    {
        var problems = new List<string>();

        if (!IdPattern().IsMatch(rule.Id ?? string.Empty))
        {
            problems.Add("id must use only uppercase letters, digits and underscores");
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            problems.Add("name is missing");
        }

        if (string.IsNullOrWhiteSpace(rule.Description))
        {
            problems.Add("description is missing");
        }

        if (!Enum.IsDefined(rule.Category))
        {
            problems.Add("category is unknown");
        }

        if (!Enum.IsDefined(rule.Risk) || rule.Risk == RiskLevel.Blocked)
        {
            problems.Add("risk must be safe, caution or expert");
        }

        if (rule.AutomaticCleaningAllowed && rule.Risk != RiskLevel.Safe)
        {
            problems.Add("automatic cleaning is only allowed for safe rules");
        }

        if (rule.MinimumAgeDays < 0)
        {
            problems.Add("minimumAgeDays cannot be negative");
        }

        if (rule.Paths is null || rule.Paths.Count == 0)
        {
            problems.Add("paths is empty");
        }
        else
        {
            foreach (var path in rule.Paths)
            {
                problems.AddRange(ValidatePath(path, expand));
            }
        }

        return problems;
    }

    private static IEnumerable<string> ValidatePath(string? path, Func<string, string> expand)
    {
        // Starting from a variable keeps rules portable and rules out absolute paths like "C:\".
        if (path is null || !PathPattern().IsMatch(path))
        {
            yield return $"path '{path}' must start with an environment variable and contain only folder names";
            yield break;
        }

        var expanded = expand(path);
        if (expanded.Contains('%'))
        {
            // The variable does not exist on this PC; the scanner will simply skip the path.
            yield break;
        }

        if (!Path.IsPathFullyQualified(expanded) && !IsDriveName(expanded))
        {
            yield return $"path '{path}' does not resolve to a full path on this PC";
            yield break;
        }

        var protectedFolder = new ProtectedPathService(expand).FindProtectedFolderWithin(expanded);
        if (protectedFolder is not null)
        {
            yield return $"path '{path}' would include the protected folder '{protectedFolder}'";
        }
    }

    private static bool IsDriveName(string path) => path.Length == 2 && path[1] == ':';

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$")]
    private static partial Regex IdPattern();

    // A variable, then folder names only: no "..", no wildcards, no drive letters.
    [GeneratedRegex(@"^%[A-Za-z_()0-9]+%(\\(?!\.\.?(\\|$))[^\\/:*?""<>|]+)*$")]
    private static partial Regex PathPattern();
}
