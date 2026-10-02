using Clean.Core.Interfaces;

namespace Clean.Core.Maintenance;

// The silent weekly pass: same scan, safety engine and cleaner as the Nettoyage page, but only the items
// MaintenancePolicy lets through, and no questions asked. Everything it moves is restorable from the history.
public sealed class MaintenanceRunner(
    IScanManager scanManager,
    IRuleEngine rules,
    ISafetyEngine safety,
    ICleaner cleaner,
    ILicenseService license,
    TimeProvider clock)
{
    public async Task<MaintenanceReport> RunAsync(string driveRoot, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (!license.Access.CanClean)
        {
            return new MaintenanceReport(now, MaintenanceStatus.LicenseRequired, 0, 0, 0, 0);
        }

        try
        {
            var scan = await scanManager.RunAsync(driveRoot, null, cancellationToken);
            var decisions = MaintenancePolicy.Select(scan.Items, rules, safety);
            if (decisions.Count == 0)
            {
                return new MaintenanceReport(now, MaintenanceStatus.NothingToClean, 0, 0, 0, 0);
            }

            var result = await cleaner.CleanAsync(decisions, null, cancellationToken);
            return new MaintenanceReport(
                now,
                result.RemovedFileCount > 0 ? MaintenanceStatus.Done : MaintenanceStatus.NothingToClean,
                result.RemovedBytes,
                result.RemovedFileCount,
                result.LockedFileCount,
                result.CleanedLocationCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Reported rather than thrown: nobody is watching a scheduled run, so the report is what they will see.
            return new MaintenanceReport(now, MaintenanceStatus.Failed, 0, 0, 0, 0);
        }
    }
}
