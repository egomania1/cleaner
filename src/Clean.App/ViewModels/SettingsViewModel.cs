using System.Windows.Input;
using Clean.Core.Interfaces;
using Clean.Infrastructure.Logging;
using CommunityToolkit.Mvvm.Input;

namespace Clean.App.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel(LicenseViewModel license, IFileExplorer explorer)
    {
        License = license;
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
}
