using System.Text.RegularExpressions;
using Clean.Core.Models;

namespace Clean.Core.Uninstall;

// The folder names a program is likely to leave behind: its display name, that name without a trailing
// version or architecture, and the folder it was installed in. Short names are too common to trust.
public static partial class LeftoverNames
{
    private const int MinimumLength = 4;

    public static IReadOnlyList<string> For(ProgramInfo program)
    {
        var names = new List<string> { program.Name };

        var simplified = Architecture().Replace(program.Name, string.Empty);
        simplified = TrailingVersion().Replace(simplified, string.Empty).Trim();
        names.Add(simplified);

        if (!string.IsNullOrWhiteSpace(program.Location))
        {
            names.Add(Path.GetFileName(program.Location.Trim().Trim('"').TrimEnd('\\', '/')));
        }

        return names
            .Select(name => name.Trim())
            .Where(name => name.Length >= MinimumLength && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [GeneratedRegex(@"\s*\((x64|x86|64-bit|32-bit)\)", RegexOptions.IgnoreCase)]
    private static partial Regex Architecture();

    [GeneratedRegex(@"[\s-]+v?\d+(\.\d+)*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingVersion();
}
