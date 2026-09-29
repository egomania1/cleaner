using Clean.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Clean.App.Controls;

public sealed partial class StorageDetailCard : UserControl
{
    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(object), typeof(StorageDetailCard), new PropertyMetadata(null, OnDetailChanged));

    public StorageDetailCard()
    {
        InitializeComponent();
    }

    public StorageDetailViewModel? Detail
    {
        get => (StorageDetailViewModel?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    private static void OnDetailChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var card = (StorageDetailCard)sender;
        if (e.NewValue is StorageDetailViewModel detail)
        {
            card.GradientBase.Background = CardGradient.CreateBase(detail.Kind, detail.Name);
            card.GradientGlow.Background = CardGradient.CreateGlow(detail.Kind, detail.Name);
        }
    }
}
