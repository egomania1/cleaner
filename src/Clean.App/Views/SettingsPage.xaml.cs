using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.RefreshAsync();
    }

    private async void OnMaintenanceToggled(object sender, RoutedEventArgs e) =>
        await ViewModel.SetAutoMaintenanceAsync(((ToggleSwitch)sender).IsOn);
}
