using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Linq;
using Clean.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Clean.Infrastructure.Settings;

// Registers a weekly Windows scheduled task through schtasks.exe. A missed run (PC off on Sunday) starts
// when the PC is next available, and nothing starts while on battery.
public sealed class MaintenanceScheduler(string executablePath, ILogger<MaintenanceScheduler> logger) : IMaintenanceScheduler
{
    public const string TaskName = "Clean - Maintenance";
    public const string Argument = "--maintenance";

    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public bool IsScheduled() => RunSchtasks($"/Query /TN \"{TaskName}\"") == 0;

    public ScheduleResult Enable()
    {
        var definition = Path.Combine(Path.GetTempPath(), $"clean-task-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks reads task definitions as UTF-16.
            BuildTaskXml(executablePath).Save(definition);
            return RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{definition}\" /F") == 0 ? ScheduleResult.Done : ScheduleResult.Failed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not write the task definition");
            return ScheduleResult.Failed;
        }
        finally
        {
            try
            {
                File.Delete(definition);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temporary file is harmless.
            }
        }
    }

    public ScheduleResult Disable()
    {
        if (!IsScheduled())
        {
            return ScheduleResult.Done;
        }

        return RunSchtasks($"/Delete /TN \"{TaskName}\" /F") == 0 ? ScheduleResult.Done : ScheduleResult.Failed;
    }

    public static XDocument BuildTaskXml(string executablePath) =>
        new(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(
                Ns + "Task",
                new XAttribute("version", "1.4"),
                new XElement(Ns + "RegistrationInfo", new XElement(Ns + "Description", "Nettoyage hebdomadaire silencieux de Clean : seuls les éléments sûrs, tout reste restaurable.")),
                new XElement(
                    Ns + "Triggers",
                    new XElement(
                        Ns + "CalendarTrigger",
                        new XElement(Ns + "StartBoundary", "2026-01-04T12:00:00"),
                        new XElement(Ns + "Enabled", "true"),
                        new XElement(
                            Ns + "ScheduleByWeek",
                            new XElement(Ns + "DaysOfWeek", new XElement(Ns + "Sunday")),
                            new XElement(Ns + "WeeksInterval", "1")))),
                new XElement(
                    Ns + "Principals",
                    new XElement(
                        Ns + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(Ns + "LogonType", "InteractiveToken"),
                        new XElement(Ns + "RunLevel", "LeastPrivilege"))),
                new XElement(
                    Ns + "Settings",
                    new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(Ns + "DisallowStartIfOnBatteries", "true"),
                    new XElement(Ns + "StopIfGoingOnBatteries", "true"),
                    new XElement(Ns + "StartWhenAvailable", "true"),
                    new XElement(Ns + "ExecutionTimeLimit", "PT1H"),
                    new XElement(Ns + "Enabled", "true")),
                new XElement(
                    Ns + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(Ns + "Exec", new XElement(Ns + "Command", executablePath), new XElement(Ns + "Arguments", Argument)))));

    private int RunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(exception, "Could not run schtasks.exe");
            return -1;
        }
    }
}
