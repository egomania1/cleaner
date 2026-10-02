using System.ComponentModel;
using System.Diagnostics;
using Clean.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Settings;

// Asks Windows for a system restore point. Creating one needs administrator rights, so PowerShell is started
// with the "runas" verb: Windows shows its own prompt and the user can refuse. Windows itself keeps at most
// one restore point per 24 hours and does nothing when System Protection is off.
public sealed class RestorePointService(ILogger<RestorePointService> logger) : IRestorePointService
{
    private const int ElevationRefused = 1223;

    public async Task<RestorePointOutcome> CreateAsync(string description, CancellationToken cancellationToken)
    {
        var safeDescription = description.Replace("'", string.Empty, StringComparison.Ordinal);
        var command = $"Checkpoint-Computer -Description '{safeDescription}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop";

        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                "powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"")
            {
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process is null)
            {
                return RestorePointOutcome.Failed;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 ? RestorePointOutcome.Created : RestorePointOutcome.Failed;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ElevationRefused)
        {
            return RestorePointOutcome.Declined;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(exception, "Could not create a restore point");
            return RestorePointOutcome.Failed;
        }
    }
}
