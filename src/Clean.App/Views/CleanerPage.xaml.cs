using Clean.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Clean.App.Views;

public sealed partial class CleanerPage : Page
{
    public CleanerPage()
    {
        InitializeComponent();
    }

    public CleanerViewModel ViewModel { get; } = App.Services.GetRequiredService<CleanerViewModel>();
}
