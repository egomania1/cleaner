using System.Collections.ObjectModel;
using Clean.App.Services;
using Clean.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clean.App.ViewModels;

public sealed class DashboardViewModel(
    IDiskService diskService,
    StorageViewModel storage,
    CleanerViewModel cleaner,
    NavigationService navigation,
    ILogger<DashboardViewModel> logger) : ObservableObject
{
    private string? _errorMessage;

    public ObservableCollection<DiskItemViewModel> Disks { get; } = [];

    public CleanerViewModel Cleaner => cleaner;

    public IAsyncRelayCommand AnalyzeCommand { get; } = new AsyncRelayCommand(async () =>
    {
        navigation.NavigateTo("storage");
        await storage.AnalyzeSystemDiskAsync();
    });

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => ErrorMessage is not null;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var disks = await diskService.GetDisksAsync(cancellationToken);

            Disks.Clear();
            foreach (var disk in disks)
            {
                Disks.Add(new DiskItemViewModel(disk));
            }

            ErrorMessage = null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogError(exception, "Could not list the disks");
            ErrorMessage = "Impossible de lire les disques de ce PC.";
        }
    }
}
