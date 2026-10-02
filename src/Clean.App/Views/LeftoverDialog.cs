using Clean.Core.Formatting;
using Clean.Core.Models;
using Clean.Core.Uninstall;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Clean.App.Views;

public static class LeftoverDialog
{
    // Returns the folders the user ticked, or an empty list. Nothing is ticked by default.
    public static async Task<IReadOnlyList<string>> ShowAsync(FrameworkElement owner, ProgramInfo program, IReadOnlyList<LeftoverFolder> folders)
    {
        var boxes = new List<(CheckBox Box, string Path)>();
        var list = new StackPanel { Spacing = 10 };
        foreach (var folder in folders)
        {
            var box = new CheckBox
            {
                MinWidth = 0,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = folder.Path, TextWrapping = TextWrapping.Wrap, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        new TextBlock
                        {
                            Text = $"{ByteSize.Format(folder.SizeBytes)} · {folder.FileCount:N0} fichier(s) · {folder.Reason}",
                            FontSize = 12,
                            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            };
            boxes.Add((box, folder.Path));
            list.Children.Add(box);
        }

        var content = new StackPanel { Spacing = 12, MaxWidth = 520 };
        content.Children.Add(new TextBlock
        {
            Text = $"{program.Name} est désinstallé, mais ces dossiers portent encore son nom. Coche ceux que tu veux mettre de côté : ils restent restaurables {CleaningSession.RetentionPeriod.Days} jours depuis l'Historique.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Un dossier peut contenir des réglages ou des sauvegardes à toi (parties de jeu, profils). Regarde avant de cocher.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["CautionBrush"],
        });
        content.Children.Add(new ScrollViewer { MaxHeight = 280, Content = list });

        var dialog = new ContentDialog
        {
            XamlRoot = owner.XamlRoot,
            RequestedTheme = owner.ActualTheme,
            Title = "Restes de " + program.Name,
            Content = content,
            PrimaryButtonText = "Mettre de côté",
            CloseButtonText = "Laisser en place",
            DefaultButton = ContentDialogButton.Close,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? boxes.Where(entry => entry.Box.IsChecked == true).Select(entry => entry.Path).ToList()
            : [];
    }
}
