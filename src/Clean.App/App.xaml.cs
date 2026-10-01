using Clean.App.Services;
using Clean.App.ViewModels;
using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;
using Clean.Core.Scanning;
using Clean.Infrastructure.Cleaning;
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
        UnhandledException += (_, e) => WriteCrashLog(e.Exception);
    }

    // The Debug logger is invisible outside Visual Studio; a crash leaves this file behind instead.
    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clean");
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "crash.log"), $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
        {
            // Nothing else can be done while the app is going down.
        }
    }

    public static IServiceProvider Services { get; } = ConfigureServices();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = Services.GetRequiredService<MainWindow>();
        _mainWindow.Activate();
        FreeExpiredArchives();
    }

    // Past the restore period, archived files only take space: it is handed back to the disk at startup.
    private static async void FreeExpiredArchives()
    {
        try
        {
            await Services.GetRequiredService<ICleaningArchive>().FreeExpiredAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Services.GetRequiredService<ILogger<App>>().LogError(exception, "Could not free the expired archives");
        }
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
        services.AddSingleton<IFolderSizer, FolderSizer>();
        services.AddSingleton<IProcessMonitor, ProcessMonitor>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<JsonRuleLoader>();
        services.AddSingleton<IRuleEngine>(provider =>
            new RuleEngine(provider.GetRequiredService<JsonRuleLoader>().Load(JsonRuleLoader.DefaultFolder).Rules));

        services.AddSingleton<IReparsePointDetector, ReparsePointDetector>();
        services.AddSingleton<IScanner>(provider => new TempScanner(
            RulesWhere(provider, rule => rule.Category == CleaningCategory.Temporary),
            provider.GetRequiredService<IReparsePointDetector>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<TempScanner>>()));
        services.AddSingleton<IScanner>(provider => new KnownLocationScanner(
            RulesWhere(provider, rule => rule.Category != CleaningCategory.Temporary),
            provider.GetRequiredService<IReparsePointDetector>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<KnownLocationScanner>>()));
        services.AddSingleton<IScanManager, ScanManager>();

        services.AddSingleton<IPathValidator>(provider => new PathValidator(
            provider.GetRequiredService<IRuleEngine>(),
            provider.GetRequiredService<IReparsePointDetector>()));
        services.AddSingleton<ISafetyEngine, SafetyEngine>();
        services.AddSingleton<ICleaningArchive>(provider => new CleaningArchive(
            CleaningArchive.DefaultFolder,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<CleaningArchive>>()));
        services.AddSingleton<ICleaner, FileCleaner>();

        services.AddSingleton(UserFileScope.ForCurrentUser(CleaningArchive.DefaultFolder));
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<IDuplicateFinder, DuplicateFinder>();
        services.AddSingleton<IFileRemover, FileRemover>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<StorageViewModel>();
        services.AddSingleton<CleanerViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<AppsViewModel>();
        services.AddSingleton<DuplicatesViewModel>();
        services.AddSingleton<LargeFilesViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static List<CleaningRule> RulesWhere(IServiceProvider provider, Func<CleaningRule, bool> predicate) =>
        provider.GetRequiredService<IRuleEngine>().Rules.Where(predicate).ToList();
}
