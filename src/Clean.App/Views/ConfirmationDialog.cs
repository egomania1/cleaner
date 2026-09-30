using Clean.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.Views;

public static class ConfirmationDialog
{
    public static async Task<bool> ShowAsync(FrameworkElement owner, CleaningConfirmation confirmation)
    {
        var content = new StackPanel { Spacing = 12, MaxWidth = 480 };
        content.Children.Add(new TextBlock { Text = confirmation.Message, TextWrapping = TextWrapping.Wrap });
        if (confirmation.Warning is not null)
        {
            content.Children.Add(new TextBlock
            {
                Text = confirmation.Warning,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["CautionBrush"],
            });
        }

        // "Annuler" is the default so that a stray Enter key never deletes anything.
        var dialog = new ContentDialog
        {
            XamlRoot = owner.XamlRoot,
            RequestedTheme = owner.ActualTheme,
            Title = confirmation.Title,
            Content = content,
            PrimaryButtonText = confirmation.ConfirmText,
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
