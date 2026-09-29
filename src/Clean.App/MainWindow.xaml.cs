using Microsoft.UI.Xaml;

namespace Clean.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetIcon("Assets/Clean.ico");
    }
}
