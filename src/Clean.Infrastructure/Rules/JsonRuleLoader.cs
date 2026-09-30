using System.Text.Json;
using System.Text.Json.Serialization;
using Clean.Core.Models;
using Clean.Core.Rules;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Rules;

// A broken or unsafe rule is skipped and reported, never loaded, and never stops the app from starting.
public sealed class JsonRuleLoader(ILogger<JsonRuleLoader> logger)
{
    public const int SupportedVersion = 1;

    public static string DefaultFolder { get; } = Path.Combine(AppContext.BaseDirectory, "Rules");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public RuleLoadResult Load(string folder) => Load(folder, Environment.ExpandEnvironmentVariables);

    public RuleLoadResult Load(string folder, Func<string, string> expand)
    {
        var rules = new List<CleaningRule>();
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(folder))
        {
            errors.Add($"Rule folder '{folder}' does not exist");
        }
        else
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.OrdinalIgnoreCase))
            {
                LoadFile(file, expand, rules, ids, errors);
            }
        }

        foreach (var error in errors)
        {
            logger.LogWarning("Rule skipped: {Error}", error);
        }

        logger.LogInformation("Loaded {Count} cleaning rules from {Folder}", rules.Count, folder);
        return new RuleLoadResult(rules, errors);
    }

    private static void LoadFile(string file, Func<string, string> expand, List<CleaningRule> rules, HashSet<string> ids, List<string> errors)
    {
        var fileName = Path.GetFileName(file);
        RuleFile? content;

        try
        {
            content = JsonSerializer.Deserialize<RuleFile>(File.ReadAllText(file), Options);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            errors.Add($"{fileName}: {exception.Message}");
            return;
        }

        if (content?.Version != SupportedVersion)
        {
            errors.Add($"{fileName}: version must be {SupportedVersion}");
            return;
        }

        foreach (var entry in content.Rules ?? [])
        {
            if (entry.Category is null || entry.Risk is null || entry.MinimumAgeDays is null || entry.AutomaticCleaningAllowed is null)
            {
                errors.Add($"{fileName}, rule '{entry.Id}': category, risk, minimumAgeDays and automaticCleaningAllowed are required");
                continue;
            }

            var rule = new CleaningRule(
                entry.Id!,
                entry.Name!,
                entry.Description!,
                entry.Category.Value,
                entry.Risk.Value,
                entry.Paths!,
                entry.MinimumAgeDays.Value,
                entry.AutomaticCleaningAllowed.Value);

            var problems = RuleValidator.Validate(rule, expand);
            if (problems.Count > 0)
            {
                errors.Add($"{fileName}, rule '{entry.Id}': {string.Join("; ", problems)}");
            }
            else if (!ids.Add(rule.Id))
            {
                errors.Add($"{fileName}, rule '{entry.Id}': this id is already used by another rule");
            }
            else
            {
                rules.Add(rule);
            }
        }
    }

    private sealed record RuleFile(int? Version, List<RuleEntry>? Rules);

    private sealed record RuleEntry(
        string? Id,
        string? Name,
        string? Description,
        CleaningCategory? Category,
        RiskLevel? Risk,
        List<string>? Paths,
        int? MinimumAgeDays,
        bool? AutomaticCleaningAllowed);
}
