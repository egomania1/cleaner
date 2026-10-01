using System.Collections.ObjectModel;
using System.Globalization;
using Clean.Core.Interfaces;
using Clean.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

public sealed class StartupViewModel(IStartupCatalog catalog, ILogger<StartupViewModel> logger) : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private bool _isLoaded;
    private string? _statusMessage;

    public ObservableCollection<StartupRow> Rows { get; } = [];

    public bool IsEmpty => _isLoaded && Rows.Count == 0;

    public string TotalText => Rows.Count.ToString("N0", French);

    public string EnabledText => Rows.Count(row => row.IsEnabled).ToString("N0", French);

    public string DisabledText => Rows.Count(row => !row.IsEnabled).ToString("N0", French);

    public string MissingText => Rows.Count(row => row.IsMissing).ToString("N0", French);

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

    public async Task LoadAsync()
    {
        try
        {
            var entries = await Task.Run(catalog.Read);
            Rows.Clear();
            foreach (var entry in entries)
            {
                Rows.Add(new StartupRow(entry, ToggleAsync));
            }

            _isLoaded = true;
            RaiseCounts();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not read the startup programs");
            StatusMessage = "Impossible de lire les programmes de démarrage.";
        }
    }

    private async Task ToggleAsync(StartupRow row)
    {
        var enable = !row.IsEnabled;
        try
        {
            var result = await Task.Run(() => catalog.SetEnabled(row.Id, enable));
            switch (result)
            {
                case StartupChangeResult.Changed:
                    row.IsEnabled = enable;
                    StatusMessage = enable
                        ? $"{row.Name} démarrera de nouveau avec Windows."
                        : $"{row.Name} ne démarrera plus avec Windows. Tu peux le réactiver ici à tout moment.";
                    RaiseCounts();
                    break;
                case StartupChangeResult.NeedsAdministrator:
                    StatusMessage = row.AdministratorText;
                    break;
                case StartupChangeResult.NotFound:
                    StatusMessage = "Cette entrée a changé entre-temps : la liste est mise à jour.";
                    await LoadAsync();
                    break;
                default:
                    StatusMessage = "Windows a refusé le changement. Rien n'a été modifié.";
                    break;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not change a startup program");
            StatusMessage = "Le changement a échoué. Rien n'a été modifié.";
        }
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(EnabledText));
        OnPropertyChanged(nameof(DisabledText));
        OnPropertyChanged(nameof(MissingText));
    }
}
