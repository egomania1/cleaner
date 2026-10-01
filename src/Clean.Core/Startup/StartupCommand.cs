namespace Clean.Core.Startup;

// A Run entry is a command line, not a path: `"C:\Program Files\App\app.exe" --minimized`, `C:\App\app.exe /silent`,
// or `%APPDATA%\App\app.exe`. This finds the program that the line starts.
public static class StartupCommand
{
    public static string? ExtractExecutable(string? command, Func<string, string>? expand = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var text = (expand ?? Environment.ExpandEnvironmentVariables)(command.Trim());

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        // Unquoted with spaces ("C:\Program Files\App\app.exe /x"): the program ends at the first ".exe".
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe > 0)
        {
            return text[..(exe + 4)];
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }
}
