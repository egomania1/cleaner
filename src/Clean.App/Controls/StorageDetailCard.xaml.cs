using Clean.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Clean.App.Controls;

public sealed partial class StorageDetailCard : UserControl
{
    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(object), typeof(StorageDetailCard), new PropertyMetadata(null));

    public StorageDetailCard()
    {
        InitializeComponent();
    }

    public StorageDetailViewModel? Detail
    {
        get => (StorageDetailViewModel?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }
}
