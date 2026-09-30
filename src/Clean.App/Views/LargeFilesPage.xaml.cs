using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class LargeFilesPage : Page
{
    public LargeFilesPage()
    {
        InitializeComponent();
    }

    public LargeFilesViewModel ViewModel { get; } = App.Services.GetRequiredService<LargeFilesViewModel>();

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

    private void OnTileSelected(object? sender, string key) => ViewModel.Focus(key);

    private void OnRowTapped(object sender, TappedRoutedEventArgs e) => ViewModel.Focus((sender as FrameworkElement)?.Tag as string);

    private void OnRevealClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string path)
        {
            ViewModel.Reveal(path);
        }
    }
}
