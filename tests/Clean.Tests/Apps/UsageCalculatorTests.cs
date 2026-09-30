using Clean.Core.Apps;
using Clean.Core.Models;

namespace Clean.Tests.Apps;

public class UsageCalculatorTests
{
    private static readonly DateTime Start = new(2026, 9, 30, 8, 0, 0);

    private static readonly UsageCalculator Calculator = new(
        [
            new ProgramInfo("Google Chrome", "Google", "1", null, @"C:\Program Files\Google\Chrome\Application"),
            new ProgramInfo("Steam", "Valve", "1", null, @"C:\Games\Steam"),
            new ProgramInfo("Counter-Strike 2", "Valve", "1", null, @"C:\Games\Steam\steamapps\common\CS2"),
            new ProgramInfo("Broken", null, null, null, @"C:\"),
            new ProgramInfo("Shared", null, null, null, @"C:\Program Files"),
        ],
        @"C:\Windows",
        [@"C:\Program Files"]);

    [Fact]
    public void Compute_GroupsTheProcessesOfOneApplication()
    {
        ProcessSample[] before = [Chrome(1, 1_000, 100), Chrome(2, 500, 50)];
        ProcessSample[] after = [Chrome(1, 1_600, 120), Chrome(2, 700, 80)];

        var usage = Assert.Single(Calculator.Compute(before, after, TimeSpan.FromSeconds(1), processorCount: 4));

        Assert.Equal("Google Chrome", usage.DisplayName);
        Assert.Equal(2, usage.ProcessCount);
        Assert.Equal(200, usage.MemoryBytes);
        Assert.Equal(20, usage.CpuPercent, 3);
    }

    [Fact]
    public void Compute_PicksTheDeepestInstalledFolder()
    {
        var game = Sample(3, @"C:\Games\Steam\steamapps\common\CS2\bin\cs2.exe", 0, 10);

        Assert.Equal("Counter-Strike 2", Assert.Single(Calculator.Compute([], [game], TimeSpan.FromSeconds(1), 1)).DisplayName);
    }

    [Fact]
    public void Compute_IgnoresDriveRootsAndSharedFolders()
    {
        var tool = Sample(4, @"C:\Program Files\Tool\tool.exe", 0, 10) with { Description = "Super Tool" };

        var usage = Assert.Single(Calculator.Compute([], [tool], TimeSpan.FromSeconds(1), 1));

        Assert.Equal("Super Tool", usage.DisplayName);
        Assert.Null(usage.Program);
    }

    [Fact]
    public void Compute_MatchesAProgramWithoutFolderByTheExecutableDescription()
    {
        var calculator = new UsageCalculator([new ProgramInfo("Google Chrome", "Google", "1", null, null)], @"C:\Windows", []);
        var chrome = Sample(8, @"C:\Program Files\Google\Chrome\Application\chrome.exe", 0, 10) with { Description = "Google Chrome" };

        var usage = Assert.Single(calculator.Compute([], [chrome], TimeSpan.FromSeconds(1), 1));

        Assert.Equal("app:Google Chrome", usage.Key);
        Assert.NotNull(usage.Program);
    }

    [Fact]
    public void Compute_PutsWindowsAndProtectedProcessesInTheirOwnGroups()
    {
        ProcessSample[] samples =
        [
            Sample(5, @"C:\Windows\explorer.exe", 0, 10),
            Sample(6, @"C:\Windows\System32\svchost.exe", 0, 10),
            Sample(7, null, 0, 30),
        ];

        var usages = Calculator.Compute([], samples, TimeSpan.FromSeconds(1), 1);

        Assert.Equal(2, usages.Single(usage => usage.Key == UsageCalculator.WindowsKey).ProcessCount);
        Assert.Equal(30, usages.Single(usage => usage.Key == UsageCalculator.ProtectedKey).MemoryBytes);
    }

    [Fact]
    public void Compute_CountsNoProcessorTimeWithoutAnEarlierMeasure()
    {
        var usage = Assert.Single(Calculator.Compute([], [Chrome(1, 50_000, 10)], TimeSpan.FromSeconds(1), 1));

        Assert.Equal(0, usage.CpuPercent);
    }

    [Fact]
    public void Compute_DoesNotMixAReusedProcessIdWithTheOldProcess()
    {
        var old = Chrome(1, 1_000, 10);
        var reused = old with { StartTime = Start.AddMinutes(5), ProcessorTime = TimeSpan.FromMilliseconds(5_000) };

        Assert.Equal(0, Assert.Single(Calculator.Compute([old], [reused], TimeSpan.FromSeconds(1), 1)).CpuPercent);
    }

    [Fact]
    public void Compute_NeverGoesAboveOneHundredPercent()
    {
        var usage = Assert.Single(Calculator.Compute([Chrome(1, 0, 1)], [Chrome(1, 10_000, 1)], TimeSpan.FromSeconds(1), 1));

        Assert.Equal(100, usage.CpuPercent);
    }

    private static ProcessSample Chrome(int id, int cpuMilliseconds, long memory) =>
        Sample(id, @"C:\Program Files\Google\Chrome\Application\chrome.exe", cpuMilliseconds, memory);

    private static ProcessSample Sample(int id, string? path, int cpuMilliseconds, long memory) =>
        new(id, Start, "process", path, null, TimeSpan.FromMilliseconds(cpuMilliseconds), memory);
}
