using Clean.App.ViewModels;
using Clean.App.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Clean.App;

public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan MinimumSplashDuration = TimeSpan.FromSeconds(1.2);

    private static readonly Dictionary<string, PlaceholderContent> UpcomingPages = new()
    {
        ["cleaner"] = new("Nettoyage", "Les fichiers temporaires et caches nettoyables, avec la raison de chaque proposition."),
        ["storage"] = new("Stockage", "La répartition de l'espace disque par catégorie : applications, jeux, vidéos, documents…"),
        ["apps"] = new("Applications", "L'espace utilisé par chaque application, séparé entre cache nettoyable et données protégées."),
        ["duplicates"] = new("Doublons", "Les fichiers identiques présents à plusieurs endroits."),
        ["large-files"] = new("Gros fichiers", "Les fichiers les plus volumineux, à trier toi-même."),
        ["developer"] = new("Développeur", "Les dossiers node_modules, bin/obj et les caches npm, NuGet ou pip."),
        ["history"] = new("Historique", "L'évolution de l'espace utilisé au fil du temps."),
        ["startup"] = new("Démarrage", "Les applications lancées au démarrage de Windows."),
        ["settings"] = new("Paramètres", "Thème, options d'analyse, exclusions et confidentialité."),
    };

    private readonly DashboardViewModel _dashboard;

    public MainWindow(DashboardViewModel dashboard)
    {
        _dashboard = dashboard;
        InitializeComponent();
        ConfigureWindow();
        Navigation.SelectedItem = DashboardItem;
    }

    private void ConfigureWindow()
    {
        AppWindow.SetIcon("Assets/Clean.ico");
        AppWindow.Resize(new SizeInt32(1180, 800));

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 960;
            presenter.PreferredMinimumHeight = 640;
        }
    }

    private async void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        await Task.WhenAll(_dashboard.LoadAsync(CancellationToken.None), Task.Delay(MinimumSplashDuration));
        await StartupOverlay.FadeOutAsync();
        RootGrid.Children.Remove(StartupOverlay);
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.IsSettingsSelected ? "settings" : args.SelectedItemContainer?.Tag as string;

        if (tag == "dashboard")
        {
            ContentFrame.Navigate(typeof(DashboardPage));
        }
        else if (tag is not null && UpcomingPages.TryGetValue(tag, out var content))
        {
            ContentFrame.Navigate(typeof(PlaceholderPage), content);
        }
    }
}
