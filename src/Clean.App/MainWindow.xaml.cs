using Clean.App.Services;
using Clean.App.Controls;
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

    private static readonly Dictionary<string, Type> Pages = new()
    {
        ["dashboard"] = typeof(DashboardPage),
        ["storage"] = typeof(StoragePage),
        ["cleaner"] = typeof(CleanerPage),
        ["history"] = typeof(HistoryPage),
    };

    private static readonly Dictionary<string, PlaceholderContent> UpcomingPages = new()
    {
        ["apps"] = new("04", "Applications", "L'espace utilisé par chaque application, séparé entre cache nettoyable et données protégées."),
        ["duplicates"] = new("05", "Doublons", "Les fichiers identiques présents à plusieurs endroits."),
        ["large-files"] = new("06", "Gros fichiers", "Les fichiers les plus volumineux, à trier toi-même."),
        ["developer"] = new("07", "Développeur", "Les dossiers node_modules, bin/obj et les caches npm, NuGet ou pip."),
        ["startup"] = new("09", "Démarrage", "Les applications lancées au démarrage de Windows."),
        ["settings"] = new("10", "Paramètres", "Thème, options d'analyse, exclusions et confidentialité."),
    };

    private static readonly NavItem[] NavItems =
    [
        new("dashboard", "Tableau de bord", "\uE80F"),
        new("cleaner", "Nettoyage", "\uE74D"),
        new("storage", "Stockage", "\uEDA2"),
        new("apps", "Applications", "\uE71D"),
        new("duplicates", "Doublons", "\uE8C8"),
        new("large-files", "Gros fichiers", "\uE8A5"),
        new("developer", "Développeur", "\uE943"),
        new("history", "Historique", "\uE81C"),
        new("startup", "Démarrage", "\uE7E8"),
        new("settings", "Paramètres", "\uE713", StartsGroup: true),
    ];

    private readonly DashboardViewModel _dashboard;

    public MainWindow(DashboardViewModel dashboard, NavigationService navigation)
    {
        _dashboard = dashboard;
        InitializeComponent();
        ConfigureWindow();
        NavBar.SetItems(NavItems);
        NavBar.Selected += ShowPage;
        navigation.NavigationRequested += NavBar.Select;
        NavBar.Select("dashboard");
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
        await StartupOverlay.CompleteAsync();
        await StartupOverlay.FadeOutAsync();
        RootGrid.Children.Remove(StartupOverlay);
    }

    private void ShowPage(string tag)
    {
        if (Pages.TryGetValue(tag, out var pageType))
        {
            ContentFrame.Navigate(pageType);
        }
        else if (UpcomingPages.TryGetValue(tag, out var content))
        {
            ContentFrame.Navigate(typeof(PlaceholderPage), content);
        }
    }
}
