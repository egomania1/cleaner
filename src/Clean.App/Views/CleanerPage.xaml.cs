using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class CleanerPage : Page
{
    public CleanerPage()
    {
        InitializeComponent();
    }

    public CleanerViewModel ViewModel { get; } = App.Services.GetRequiredService<CleanerViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureDisksLoadedAsync();
    }
}
