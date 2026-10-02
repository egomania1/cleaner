using System.Globalization;
using System.Windows.Input;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Maintenance;
using Clean.Infrastructure.Logging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clean.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly ISettingsStore _settings;
    private readonly IMaintenanceScheduler _scheduler;
    private readonly IMaintenanceReportStore _reports;
    private bool _autoMaintenance;
    private string? _maintenanceMessage;
    private string _lastReportText = string.Empty;

    public SettingsViewModel(
        LicenseViewModel license,
        IFileExplorer explorer,
        ISettingsStore settings,
        IMaintenanceScheduler scheduler,
        IMaintenanceReportStore reports)
    {
        License = license;
        _settings = settings;
        _scheduler = scheduler;
        _reports = reports;
        OpenLogsCommand = new RelayCommand(() =>
        {
            Directory.CreateDirectory(FileLoggerProvider.DefaultFolder);
            explorer.Reveal(FileLoggerProvider.DefaultFolder);
        });
    }

    public LicenseViewModel License { get; }

    public ICommand OpenLogsCommand { get; }

    public string LogsText =>
        $"Clean garde un journal technique sur ce PC, {FileLoggerProvider.Retention.Days} jours au plus, sans ton nom ni ton dossier personnel. " +
        "Il n'est jamais envoyé : tu peux le joindre toi-même si tu demandes de l'aide.";

    public bool RestorePointBeforeCleaning
    {
        get => _settings.Current.RestorePointBeforeCleaning;
        set
        {
            if (value != _settings.Current.RestorePointBeforeCleaning)
            {
                _settings.Save(_settings.Current with { RestorePointBeforeCleaning = value });
                OnPropertyChanged();
            }
        }
    }

    public string RestorePointText =>
        "Avant chaque nettoyage, Clean demande à Windows un point de restauration (Windows affiche sa demande d'autorisation). " +
        "Windows n'en garde qu'un par jour et rien si la protection du système est coupée. Les fichiers retirés par Clean, eux, sont déjà restaurables depuis l'Historique.";

    public bool AutoMaintenance
    {
        get => _autoMaintenance;
        private set => SetProperty(ref _autoMaintenance, value);
    }

    public string MaintenanceText =>
        "Une fois par semaine, le dimanche à midi (ou dès que le PC est disponible, hors batterie), Clean nettoie en silence les éléments marqués sûrs et rien d'autre. " +
        "Les navigateurs ouverts sont laissés tranquilles. Tout ce qui est retiré apparaît dans l'Historique et reste restaurable.";

    public string? MaintenanceMessage
    {
        get => _maintenanceMessage;
        private set
        {
            if (SetProperty(ref _maintenanceMessage, value))
            {
                OnPropertyChanged(nameof(HasMaintenanceMessage));
            }
        }
    }

    public bool HasMaintenanceMessage => !string.IsNullOrEmpty(_maintenanceMessage);

    public string LastReportText
    {
        get => _lastReportText;
        private set => SetProperty(ref _lastReportText, value);
    }

    public async Task RefreshAsync()
    {
        License.Refresh();
        AutoMaintenance = await Task.Run(_scheduler.IsScheduled);
        LastReportText = DescribeLastReport();
    }

    public async Task SetAutoMaintenanceAsync(bool enabled)
    {
        if (enabled == AutoMaintenance)
        {
            return;
        }

        var result = await Task.Run(() => enabled ? _scheduler.Enable() : _scheduler.Disable());
        if (result == ScheduleResult.Failed)
        {
            MaintenanceMessage = "Windows a refusé de programmer la tâche. Rien n'a changé.";
            OnPropertyChanged(nameof(AutoMaintenance));
            return;
        }

        AutoMaintenance = enabled;
        _settings.Save(_settings.Current with { AutoMaintenance = enabled });
        MaintenanceMessage = enabled
            ? "Maintenance automatique activée. Tu peux la couper ici à tout moment."
            : "Maintenance automatique coupée.";
    }

    private string DescribeLastReport()
    {
        if (_reports.Last is not { } report)
        {
            return "Aucun passage pour l'instant.";
        }

        var when = report.RanAt.ToLocalTime().ToString("dddd d MMMM yyyy 'à' HH:mm", French);
        return report.Status switch
        {
            MaintenanceStatus.Done => $"Dernier passage le {when} : {ByteSize.Format(report.FreedBytes)} mis de côté ({report.FileCount.ToString("N0", French)} fichiers).",
            MaintenanceStatus.NothingToClean => $"Dernier passage le {when} : rien à nettoyer.",
            MaintenanceStatus.LicenseRequired => $"Dernier passage le {when} : le nettoyage demande la licence, rien n'a été touché.",
            _ => $"Dernier passage le {when} : une erreur a arrêté le passage, rien n'a été perdu.",
        };
    }
}
