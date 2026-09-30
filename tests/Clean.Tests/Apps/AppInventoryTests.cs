using Clean.Core.Apps;
using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Apps;

public class AppInventoryTests
{
    private static readonly string[] Shared = [@"C:\Program Files", @"C:\Users\Test\AppData\Local"];

    [Fact]
    public void Build_KeepsOneEntryPerNameAndPrefersTheCompleteOne()
    {
        var entries = AppInventory.Build(
            [
                new ProgramInfo("Tool", null, null, null, null),
                new ProgramInfo("tool", "Acme", "2", null, @"C:\Tools\Tool", 1_000),
            ],
            Shared);

        var entry = Assert.Single(entries);
        Assert.Equal(@"C:\Tools\Tool", entry.MeasureFolder);
    }

    [Fact]
    public void Build_ExcludesGamesNestedInTheLauncherFolder()
    {
        var entries = AppInventory.Build(
            [
                new ProgramInfo("Steam", "Valve", null, null, @"C:\Games\Steam"),
                new ProgramInfo("Counter-Strike 2", "Valve", null, null, @"C:\Games\Steam\steamapps\common\CS2"),
            ],
            Shared);

        var steam = entries.Single(entry => entry.Program.Name == "Steam");
        Assert.Equal([@"C:\Games\Steam\steamapps\common\CS2"], steam.ExcludedFolders);
        Assert.Empty(entries.Single(entry => entry.Program.Name == "Counter-Strike 2").ExcludedFolders);
    }

    [Fact]
    public void Build_NeverMeasuresSharedFoldersOrDriveRoots()
    {
        var entries = AppInventory.Build(
            [
                new ProgramInfo("Vendor pack", null, null, null, @"C:\Program Files\", 5_000),
                new ProgramInfo("Whole drive", null, null, null, @"D:\", 5_000),
            ],
            Shared);

        Assert.All(entries, entry => Assert.Null(entry.MeasureFolder));
    }

    [Fact]
    public void Build_GivesASharedFolderToTheBiggestAppOnly()
    {
        var entries = AppInventory.Build(
            [
                new ProgramInfo("Helper", null, null, null, @"C:\Apps\Suite", 10),
                new ProgramInfo("Suite", null, null, null, @"C:\Apps\Suite", 9_000),
            ],
            Shared);

        Assert.Equal(@"C:\Apps\Suite", entries.Single(entry => entry.Program.Name == "Suite").MeasureFolder);
        var helper = entries.Single(entry => entry.Program.Name == "Helper");
        Assert.Null(helper.MeasureFolder);
        Assert.Equal("Suite", helper.SharesFolderWith);
    }

    [Theory]
    [InlineData("Steam", AppGroup.Games)]
    [InlineData("Visual Studio Code", AppGroup.Development)]
    [InlineData("Discord", AppGroup.WebAndChat)]
    public void AppGroups_FollowTheKnownAppCategory(string name, AppGroup expected)
    {
        Assert.Equal(expected, AppGroups.Of(AppGuide.Find(name)));
    }

    [Fact]
    public void AppGroups_PutsUnknownHardwareVendorAppsInSystem()
    {
        Assert.Equal(AppGroup.SystemAndDrivers, AppGroups.Of(null, "Realtek Semiconductor Corp."));
        Assert.Equal(AppGroup.Other, AppGroups.Of(null, "Some Studio"));
    }
}
