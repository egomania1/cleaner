using System.Globalization;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Uninstall;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

// Uninstalls an application with its own uninstaller, then offers to set aside the folders it left behind.
// Nothing is removed without the user ticking it, and everything stays restorable from the history.
public sealed class UninstallViewModel(
    IProgramUninstaller uninstaller,
    ILeftoverFinder finder,
    ILeftoverRemover remover,
    ILicenseService license,
    ILogger<UninstallViewModel> logger) : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private bool _isBusy;
    private string? _statusMessage;

    public Func<CleaningConfirmation, Task<bool>>? ConfirmAsync { get; set; }

    public Func<ProgramInfo, IReadOnlyList<LeftoverFolder>, Task<IReadOnlyList<string>>>? PickLeftoversAsync { get; set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    // Returns true once the application is really gone, so the caller can drop it from its list.
    public async Task<bool> RunAsync(ProgramInfo program)
    {
        if (IsBusy || ConfirmAsync is null)
        {
            return false;
        }

        var confirmation = new CleaningConfirmation(
            $"Désinstaller {program.Name} ?",
            $"Clean lance le désinstalleur de {program.Name}, comme Windows le ferait. Ensuite, il cherche les dossiers laissés derrière et te les montre : rien n'est retiré sans que tu le coches.",
            null,
            "Désinstaller");
        if (!await ConfirmAsync(confirmation))
        {
            return false;
        }

        IsBusy = true;
        try
        {
            StatusMessage = $"Désinstallation de {program.Name} en cours… Termine la fenêtre du désinstalleur.";
            switch (await uninstaller.RunAsync(program, CancellationToken.None))
            {
                case UninstallOutcome.NoCommand:
                    StatusMessage = $"{program.Name} n'a pas de désinstalleur enregistré. Utilise « Désinstaller dans Windows ».";
                    return false;
                case UninstallOutcome.CouldNotStart:
                    StatusMessage = "Le désinstalleur n'a pas pu démarrer (ou l'autorisation a été refusée). Rien n'a été modifié.";
                    return false;
            }

            StatusMessage = "Recherche des restes…";
            var search = await finder.FindAsync(program, CancellationToken.None);
            if (search.StillInstalled)
            {
                StatusMessage = $"{program.Name} est toujours installé : le désinstalleur a été annulé ou travaille encore en arrière-plan.";
                return false;
            }

            await OfferLeftoversAsync(program, search.Folders);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Uninstall flow failed for {Program}", program.Name);
            StatusMessage = "Quelque chose s'est mal passé. Ce qui a déjà été retiré est dans l'Historique.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OfferLeftoversAsync(ProgramInfo program, IReadOnlyList<LeftoverFolder> folders)
    {
        if (folders.Count == 0)
        {
            StatusMessage = $"{program.Name} est désinstallé. Aucun dossier laissé derrière.";
            return;
        }

        if (!license.Access.CanClean)
        {
            StatusMessage = $"{program.Name} est désinstallé. {folders.Count.ToString("N0", French)} dossier(s) traînent encore : retirer les restes demande la licence (Paramètres).";
            return;
        }

        var chosen = PickLeftoversAsync is null ? [] : await PickLeftoversAsync(program, folders);
        if (chosen.Count == 0)
        {
            StatusMessage = $"{program.Name} est désinstallé. Les restes ont été laissés où ils sont.";
            return;
        }

        StatusMessage = "Mise de côté des restes…";
        var result = await remover.RemoveAsync(program, chosen, null, CancellationToken.None);
        var skipped = result.LockedFileCount > 0
            ? $" {result.LockedFileCount.ToString("N0", French)} fichier(s) n'ont pas pu bouger (ouverts, ou réservés à un administrateur)."
            : string.Empty;
        StatusMessage = $"{program.Name} est désinstallé. {ByteSize.Format(result.RemovedBytes)} de restes mis de côté, restaurables {CleaningSession.RetentionPeriod.Days} jours depuis l'Historique.{skipped}";
    }
}
