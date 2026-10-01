namespace Clean.Core.Licensing;

public static class LicenseEvaluator
{
    public const string Price = "3,90 €";

    public static readonly TimeSpan TrialLength = TimeSpan.FromDays(5);

    // A clock that moves back a little is normal (time zone fix, sync); going back days is a way to extend the trial.
    public static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromDays(2);

    public static AppAccess Evaluate(LicenseCheck? license, TrialRecord trial, DateTimeOffset now)
    {
        if (license is { AllowsPaidFeatures: true })
        {
            if (license.State == LicenseState.Grace)
            {
                return new AppAccess(AccessMode.Licensed, 0, "Licence expirée", license.Reason);
            }

            return license.Token?.Plan == LicensePlans.Owner
                ? new AppAccess(AccessMode.Licensed, 0, "Compte propriétaire", "Accès complet, sans limite de durée.")
                : new AppAccess(AccessMode.Licensed, 0, "Licence active", "Merci ! Toutes les fonctions sont disponibles.");
        }

        if (now < trial.LastSeenAt - ClockRollbackTolerance)
        {
            return Ended("La date de ce PC a été reculée, l'essai ne peut pas continuer. ");
        }

        var remaining = trial.StartedAt + TrialLength - now;
        if (remaining <= TimeSpan.Zero)
        {
            return Ended(string.Empty);
        }

        var days = (int)Math.Ceiling(remaining.TotalDays);
        return new AppAccess(
            AccessMode.Trial,
            days,
            "Essai gratuit",
            $"{days} jour{(days > 1 ? "s" : string.Empty)} restant{(days > 1 ? "s" : string.Empty)}, avec toutes les fonctions. " +
            $"Ensuite l'analyse reste gratuite ; mettre de côté ou retirer des fichiers demande la licence ({Price}, achat unique).");
    }

    private static AppAccess Ended(string prefix) => new(
        AccessMode.TrialEnded,
        0,
        "Essai terminé",
        prefix +
        "L'analyse, l'historique et la restauration restent gratuits. " +
        $"Pour mettre de côté ou retirer des fichiers, active ta licence ({Price}, achat unique).");
}
