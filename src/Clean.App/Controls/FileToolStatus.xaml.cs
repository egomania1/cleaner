using Clean.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Clean.App.Controls;

// The analysis progress, removal progress and removal result shared by the Doublons and Gros fichiers pages.
public sealed partial class FileToolStatus : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(FileToolViewModel), typeof(FileToolStatus), new PropertyMetadata(null, (sender, _) => ((FileToolStatus)sender).Bindings.Update()));

    public FileToolStatus()
    {
        InitializeComponent();
    }

    public FileToolViewModel? ViewModel
    {
        get => (FileToolViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public string SecondaryLabel { get; set; } = string.Empty;
}
