using Clean.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clean.App.ViewModels;

public sealed class StartupRow : ObservableObject
{
    private readonly StartupEntry _entry;
    private bool _isEnabled;

    public StartupRow(StartupEntry entry, Func<StartupRow, Task> toggle)
    {
        _entry = entry;
        _isEnabled = entry.IsEnabled;
        ToggleCommand = new AsyncRelayCommand(() => toggle(this), () => entry.CanChange);
    }

    public IAsyncRelayCommand ToggleCommand { get; }

    public string Id => _entry.Id;

    public bool CanChange => _entry.CanChange;

    public bool CannotChange => !_entry.CanChange;

    public bool IsMissing => _entry.TargetMissing;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(ActionText));
                OnPropertyChanged(nameof(StateLabel));
            }
        }
    }

    public string Name => _entry.Description is { Length: > 0 } description && !description.Equals(_entry.Name, StringComparison.OrdinalIgnoreCase)
        ? description
        : _entry.Name;

    public string PublisherText => _entry.Publisher ?? "Éditeur inconnu";

    public string StateText => IsEnabled ? "DÉMARRE AVEC WINDOWS" : "DÉSACTIVÉ";

    // Read by screen readers instead of the colour that sets the two states apart.
    public string StateLabel => IsEnabled ? "Démarre avec Windows" : "Désactivé";

    public string ActionText => IsEnabled ? "Désactiver" : "Activer";

    public string SourceText => _entry.Source switch
    {
        StartupSource.UserRegistry => "Ton compte",
        StartupSource.MachineRegistry => "Tous les comptes",
        StartupSource.MachineRegistry32 => "Tous les comptes (32 bits)",
        StartupSource.UserFolder => "Dossier Démarrage de ton compte",
        _ => "Dossier Démarrage de tous les comptes",
    };

    public string CommandText => _entry.Command;

    public string MissingText => "Le programme est introuvable : cette entrée ne sert plus à rien.";

    public string AdministratorText => "Pour changer cette entrée, ouvre le Gestionnaire des tâches > Applications de démarrage (droits administrateur).";
}
