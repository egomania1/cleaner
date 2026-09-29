using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Clean.App.Views;

public sealed partial class StoragePage : Page
{
    public StoragePage()
    {
        InitializeComponent();
    }

    public StorageViewModel ViewModel { get; } = App.Services.GetRequiredService<StorageViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureDisksLoadedAsync();
    }
}
