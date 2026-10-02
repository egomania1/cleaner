using Clean.Core.Interfaces;
using Clean.Core.Licensing;
using Clean.Core.Maintenance;
using Clean.Core.Models;
using Clean.Core.Settings;
using Clean.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clean.Tests.Maintenance;

public sealed class MaintenanceTests : IDisposable
{
    private readonly TestDirectory _folder = new();
    private readonly FakeRules _rules = new();
    private readonly FakeSafety _safety = new();
    private readonly FakeScan _scan = new();
    private readonly FakeCleaner _cleaner = new();
    private readonly FakeLicense _license = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Select_KeepsOnlySafeItemsOfRulesAllowedToRunAutomatically()
    {
        _rules.Rules =
        [
            Rule("AUTO_SAFE", RiskLevel.Safe, automatic: true),
            Rule("MANUAL_SAFE", RiskLevel.Safe, automatic: false),
            Rule("AUTO_CAUTION", RiskLevel.Caution, automatic: true),
        ];
        var items = new[]
        {
            Item("AUTO_SAFE", RiskLevel.Safe),
            Item("MANUAL_SAFE", RiskLevel.Safe),
            Item("AUTO_CAUTION", RiskLevel.Caution),
            Item("AUTO_SAFE", RiskLevel.Caution),
            Item("UNKNOWN_RULE", RiskLevel.Safe),
        };

        var decisions = MaintenancePolicy.Select(items, _rules, _safety);

        var decision = Assert.Single(decisions);
        Assert.Equal("AUTO_SAFE", decision.Item.RuleId);
        Assert.Equal(RiskLevel.Safe, decision.Item.Risk);
    }

    [Fact]
    public void Select_DropsWhatTheSafetyEngineRefuses()
    {
        _rules.Rules = [Rule("AUTO_SAFE", RiskLevel.Safe, automatic: true)];
        _safety.Refuse = true;

        Assert.Empty(MaintenancePolicy.Select([Item("AUTO_SAFE", RiskLevel.Safe)], _rules, _safety));
    }

    [Fact]
    public void Select_DropsItemsThatNeedAConfirmationOrHaveNothingToClean()
    {
        _rules.Rules = [Rule("AUTO_SAFE", RiskLevel.Safe, automatic: true)];
        var needsConfirmation = Item("AUTO_SAFE", RiskLevel.Safe) with { RequiresConfirmation = true };
        var nothing = Item("AUTO_SAFE", RiskLevel.Safe) with { CanClean = false };

        Assert.Empty(MaintenancePolicy.Select([needsConfirmation, nothing], _rules, _safety));
    }

    [Fact]
    public async Task RunAsync_CleansTheSelectedItemsAndReportsTheResult()
    {
        _rules.Rules = [Rule("AUTO_SAFE", RiskLevel.Safe, automatic: true)];
        _scan.Items = [Item("AUTO_SAFE", RiskLevel.Safe)];
        _cleaner.Result = new CleaningResult(5_000, 12, 2, 0, 1, false, TimeSpan.Zero, null);

        var report = await Runner().RunAsync(@"C:\", CancellationToken.None);

        Assert.Equal(MaintenanceStatus.Done, report.Status);
        Assert.Equal(5_000, report.FreedBytes);
        Assert.Equal(12, report.FileCount);
        Assert.Equal(2, report.LockedFileCount);
        Assert.Equal(1, _cleaner.Calls);
    }

    [Fact]
    public async Task RunAsync_DoesNotCleanWithoutALicenceOrTrial()
    {
        _license.CanClean = false;
        _rules.Rules = [Rule("AUTO_SAFE", RiskLevel.Safe, automatic: true)];
        _scan.Items = [Item("AUTO_SAFE", RiskLevel.Safe)];

        var report = await Runner().RunAsync(@"C:\", CancellationToken.None);

        Assert.Equal(MaintenanceStatus.LicenseRequired, report.Status);
        Assert.Equal(0, _scan.Calls);
        Assert.Equal(0, _cleaner.Calls);
    }

    [Fact]
    public async Task RunAsync_ReportsNothingToCleanWithoutCallingTheCleaner()
    {
        _scan.Items = [Item("AUTO_SAFE", RiskLevel.Safe)];

        var report = await Runner().RunAsync(@"C:\", CancellationToken.None);

        Assert.Equal(MaintenanceStatus.NothingToClean, report.Status);
        Assert.Equal(0, _cleaner.Calls);
    }

    [Fact]
    public async Task RunAsync_ReportsAFailureInsteadOfThrowing()
    {
        _rules.Rules = [Rule("AUTO_SAFE", RiskLevel.Safe, automatic: true)];
        _scan.Items = [Item("AUTO_SAFE", RiskLevel.Safe)];
        _cleaner.Throw = true;

        var report = await Runner().RunAsync(@"C:\", CancellationToken.None);

        Assert.Equal(MaintenanceStatus.Failed, report.Status);
    }

    [Fact]
    public void SettingsStore_KeepsTheChoicesAcrossRestarts()
    {
        var path = Path.Combine(_folder.RootPath, "settings.json");

        new SettingsStore(path).Save(new AppSettings(RestorePointBeforeCleaning: true, AutoMaintenance: true));

        var reloaded = new SettingsStore(path).Current;
        Assert.True(reloaded.RestorePointBeforeCleaning);
        Assert.True(reloaded.AutoMaintenance);
    }

    [Fact]
    public void SettingsStore_StartsWithEverythingOffWhenTheFileIsMissingOrBroken()
    {
        var missing = new SettingsStore(Path.Combine(_folder.RootPath, "none.json")).Current;
        var brokenPath = Path.Combine(_folder.RootPath, "broken.json");
        File.WriteAllText(brokenPath, "{ not json");
        var broken = new SettingsStore(brokenPath).Current;

        Assert.Equal(new AppSettings(), missing);
        Assert.Equal(new AppSettings(), broken);
    }

    [Fact]
    public void ReportStore_ReadsBackTheLastReport()
    {
        var store = new MaintenanceReportStore(Path.Combine(_folder.RootPath, "report.json"));
        Assert.Null(store.Last);

        var report = new MaintenanceReport(DateTimeOffset.Parse("2026-10-04T12:00:00+02:00"), MaintenanceStatus.Done, 9_000, 30, 1, 2);
        store.Save(report);

        Assert.Equal(report, store.Last);
    }

    [Fact]
    public void TaskDefinition_RunsClean_WeeklyWithoutAdministratorRightsAndNotOnBattery()
    {
        var xml = MaintenanceScheduler.BuildTaskXml(@"C:\Apps\Clean & Co\Clean.exe").ToString();

        Assert.Contains("<Command>C:\\Apps\\Clean &amp; Co\\Clean.exe</Command>", xml);
        Assert.Contains("<Arguments>--maintenance</Arguments>", xml);
        Assert.Contains("<RunLevel>LeastPrivilege</RunLevel>", xml);
        Assert.Contains("<Sunday", xml);
        Assert.Contains("<StartWhenAvailable>true</StartWhenAvailable>", xml);
        Assert.Contains("<DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries>", xml);
        Assert.Contains("<MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>", xml);
    }

    [Fact]
    public void Scheduler_CanBeConstructedWithoutTouchingWindows()
    {
        var scheduler = new MaintenanceScheduler(@"C:\x\Clean.exe", NullLogger<MaintenanceScheduler>.Instance);

        Assert.NotNull(scheduler);
    }

    private MaintenanceRunner Runner() => new(_scan, _rules, _safety, _cleaner, _license, TimeProvider.System);

    private static CleaningRule Rule(string id, RiskLevel risk, bool automatic) =>
        new(id, id, "test", CleaningCategory.Temporary, risk, [@"%TEMP%"], 0, automatic);

    private static ScanItem Item(string ruleId, RiskLevel risk) =>
        TestItems.Create(risk: risk) with { RuleId = ruleId };

    private sealed class FakeRules : IRuleEngine
    {
        public IReadOnlyList<CleaningRule> Rules { get; set; } = [];

        public CleaningRule? FindRuleFor(string path) => null;
    }

    private sealed class FakeSafety : ISafetyEngine
    {
        public bool Refuse { get; set; }

        public CleaningDecision Evaluate(ScanItem item) =>
            Refuse ? CleaningDecision.Deny(item, "no") : CleaningDecision.Allow(item, "ok");
    }

    private sealed class FakeScan : IScanManager
    {
        public IReadOnlyList<ScanItem> Items { get; set; } = [];

        public int Calls { get; private set; }

        public IReadOnlyList<IScanner> Scanners => [];

        public Task<ScanResult> RunAsync(string driveRoot, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ScanResult(Items, [], TimeSpan.Zero));
        }
    }

    private sealed class FakeCleaner : ICleaner
    {
        public CleaningResult Result { get; set; } = new(0, 0, 0, 0, 0, false, TimeSpan.Zero, null);

        public bool Throw { get; set; }

        public int Calls { get; private set; }

        public Task<CleaningResult> CleanAsync(IReadOnlyList<CleaningDecision> decisions, IProgress<CleaningProgress>? progress, CancellationToken cancellationToken)
        {
            Calls++;
            return Throw ? throw new IOException("disk error") : Task.FromResult(Result);
        }
    }

    private sealed class FakeLicense : ILicenseService
    {
        public bool CanClean { get; set; } = true;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public AppAccess Access => new(CanClean ? AccessMode.Licensed : AccessMode.TrialEnded, 0, string.Empty, string.Empty);

        public string DeviceId => "test";

        public bool CanVerifyLicenses => true;

        public void Refresh()
        {
        }

        public LicenseCheck Activate(string token) => throw new NotSupportedException();

        public void Deactivate()
        {
        }
    }
}
