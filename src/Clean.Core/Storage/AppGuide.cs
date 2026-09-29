namespace Clean.Core.Storage;

public static class AppGuide
{
    // Short keys such as "lol" or "obs" must match exactly; longer ones may also start a name
    // ("FiveM.app" → "fivemapp", "Discord Canary" → "discordcanary").
    private const int PrefixMatchMinimumLength = 5;

    public static AppProfile? Find(string? name)
    {
        var candidate = Normalize(name);
        if (candidate.Length == 0)
        {
            return null;
        }

        return AppCatalog.Profiles.FirstOrDefault(profile => profile.Keys.Any(key => Matches(candidate, Normalize(key))));
    }

    private static bool Matches(string candidate, string key) =>
        candidate == key || (key.Length >= PrefixMatchMinimumLength && candidate.StartsWith(key, StringComparison.Ordinal));

    private static string Normalize(string? text) =>
        text is null ? string.Empty : new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
