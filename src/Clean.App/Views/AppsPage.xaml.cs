using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class AppsPage : Page
{
    public AppsPage()
    {
        InitializeComponent();
    }

    public AppsViewModel ViewModel { get; } = App.Services.GetRequiredService<AppsViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureLoadedAsync();
        ViewModel.StartMonitoring();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.StopMonitoring();
    }

    private void OnTileSelected(object? sender, string key) => ViewModel.Select(key);

    private void OnAppClicked(object sender, RoutedEventArgs e) => ViewModel.Select((sender as FrameworkElement)?.Tag as string);
}
