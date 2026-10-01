using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Clean.App.Controls;

public sealed partial class LicenseBanner : UserControl
{
    public LicenseBanner()
    {
        InitializeComponent();
    }

    public LicenseViewModel ViewModel { get; } = App.Services.GetRequiredService<LicenseViewModel>();

    private void OnLoaded(object sender, RoutedEventArgs e) => ViewModel.Refresh();
}
