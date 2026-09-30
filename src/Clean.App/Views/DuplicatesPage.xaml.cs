using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class DuplicatesPage : Page
{
    public DuplicatesPage()
    {
        InitializeComponent();
    }

    public DuplicatesViewModel ViewModel { get; } = App.Services.GetRequiredService<DuplicatesViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.ConfirmAsync = confirmation => ConfirmationDialog.ShowAsync(this, confirmation);
        await ViewModel.EnsureDisksLoadedAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.ConfirmAsync = null;
    }

    private void OnRevealClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string path)
        {
            ViewModel.Reveal(path);
        }
    }
}
