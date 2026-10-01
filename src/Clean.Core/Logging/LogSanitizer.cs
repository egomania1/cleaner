namespace Clean.Core.Logging;

// A log file may be sent to support one day: the user's name and profile folder never go into it.
public sealed class LogSanitizer(string userProfile, string userName)
{
    private const int MinimumNameLength = 3;

    public static LogSanitizer ForCurrentUser() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.UserName);

    public string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = text;
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            result = result.Replace(userProfile.TrimEnd('\\'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        // A very short name would also match ordinary words.
        if (userName.Length >= MinimumNameLength)
        {
            result = result.Replace(userName, "<user>", StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
