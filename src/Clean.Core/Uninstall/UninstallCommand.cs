namespace Clean.Core.Uninstall;

public sealed record UninstallCommand(string FileName, string Arguments)
{
    // Reads the "UninstallString" an installer registered: a quoted program path, or an unquoted one that
    // may contain spaces ("C:\Program Files\X\uninstall.exe /S"). Anything without an .exe is refused.
    public static UninstallCommand? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var command = text.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end < 2)
            {
                return null;
            }

            return Build(command[1..end], command[(end + 1)..]);
        }

        var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        while (exe >= 0)
        {
            var afterExe = exe + 4;
            if (afterExe == command.Length || command[afterExe] == ' ')
            {
                return Build(command[..afterExe], command[afterExe..]);
            }

            exe = command.IndexOf(".exe", afterExe, StringComparison.OrdinalIgnoreCase);
        }

        return null;
    }

    private static UninstallCommand? Build(string fileName, string arguments) =>
        fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? new UninstallCommand(fileName, arguments.Trim()) : null;
}
