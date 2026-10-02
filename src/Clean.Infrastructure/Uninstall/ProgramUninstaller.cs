using System.ComponentModel;
using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Uninstall;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Uninstall;

// Runs the program's own uninstaller, exactly as Settings › Apps would, and waits for it to end.
// UseShellExecute lets Windows show its administrator prompt when the uninstaller asks for it.
public sealed class ProgramUninstaller(ILogger<ProgramUninstaller> logger) : IProgramUninstaller
{
    public async Task<UninstallOutcome> RunAsync(ProgramInfo program, CancellationToken cancellationToken)
    {
        if (UninstallCommand.Parse(program.UninstallCommand) is not { } command)
        {
            return UninstallOutcome.NoCommand;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(command.FileName, command.Arguments) { UseShellExecute = true });
            if (process is null)
            {
                return UninstallOutcome.CouldNotStart;
            }

            await process.WaitForExitAsync(cancellationToken);
            return UninstallOutcome.Finished;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // Includes the user refusing the administrator prompt.
            logger.LogWarning(exception, "Could not run the uninstaller of {Program}", program.Name);
            return UninstallOutcome.CouldNotStart;
        }
    }
}
