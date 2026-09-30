using Clean.App.Services;
using Clean.App.ViewModels;
using Clean.Core.Interfaces;
using Clean.Core.Rules;
using Clean.Core.Scanning;
using Clean.Infrastructure.FileSystem;
using Clean.Infrastructure.Rules;
using Clean.Infrastructure.Scanning;
using Clean.Infrastructure.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Clean.App;

public partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        InitializeComponent();
    }

    public static IServiceProvider Services { get; } = ConfigureServices();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = Services.GetRequiredService<MainWindow>();
        _mainWindow.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());

        services.AddSingleton<IDiskService, DiskService>();
        services.AddSingleton<IStorageAnalyzer, StorageAnalyzer>();
        services.AddSingleton<IFileExplorer, FileExplorer>();
        services.AddSingleton<IInstalledProgramCatalog, InstalledProgramCatalog>();
        services.AddSingleton<IEntryInspector, EntryInspector>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<JsonRuleLoader>();
        services.AddSingleton<IRuleEngine>(provider =>
            new RuleEngine(provider.GetRequiredService<JsonRuleLoader>().Load(JsonRuleLoader.DefaultFolder).Rules));

        services.AddSingleton<IScanner>(provider => new KnownLocationScanner(
            provider.GetRequiredService<IRuleEngine>().Rules,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<KnownLocationScanner>>()));
        services.AddSingleton<IScanManager, ScanManager>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StorageViewModel>();
        services.AddSingleton<CleanerViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
