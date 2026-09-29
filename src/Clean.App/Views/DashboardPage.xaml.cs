using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Clean.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    public DashboardViewModel ViewModel { get; } = App.Services.GetRequiredService<DashboardViewModel>();
}
