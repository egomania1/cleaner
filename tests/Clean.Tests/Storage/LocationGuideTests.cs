using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Storage;

public class LocationGuideTests
{
    [Theory]
    [InlineData(@"C:\Windows", RiskLevel.Blocked)]
    [InlineData(@"C:\windows", RiskLevel.Blocked)]
    [InlineData(@"C:\Program Files", RiskLevel.Blocked)]
    [InlineData(@"C:\Users", RiskLevel.Caution)]
    [InlineData(@"C:\$Recycle.Bin", RiskLevel.Safe)]
    [InlineData(@"C:\Windows\WinSxS", RiskLevel.Caution)]
    [InlineData(@"C:\Windows\Installer", RiskLevel.Blocked)]
    [InlineData(@"C:\Users\Kelia\AppData\Local\Temp", RiskLevel.Safe)]
    public void Describe_KnownFolders(string path, RiskLevel expectedRisk)
    {
        var description = LocationGuide.Describe(Folder(path));

        Assert.Equal(expectedRisk, description.Risk);
    }

    [Fact]
    public void Describe_MatchesTheFullLocation_NotJustTheName()
    {
        var systemFolder = LocationGuide.Describe(Folder(@"C:\Windows"));
        var lookalike = LocationGuide.Describe(Folder(@"D:\Backup\Windows"));

        Assert.Equal(LocationKind.System, systemFolder.Kind);
        Assert.Equal(LocationKind.Folder, lookalike.Kind);
    }

    [Fact]
    public void Describe_InsertsTheEntryName()
    {
        var application = LocationGuide.Describe(Folder(@"C:\Program Files\Google"));
        var profile = LocationGuide.Describe(Folder(@"C:\Users\Kelia"));

        Assert.Contains("Google", application.Summary);
        Assert.Contains("Kelia", profile.Summary);
    }

    [Fact]
    public void Describe_PrefersSpecificRulesOverWildcards()
    {
        var description = LocationGuide.Describe(Folder(@"C:\Users\Public"));

        Assert.Contains("partagé", description.Summary);
    }

    [Theory]
    [InlineData(@"F:\SteamLibrary\steamapps\common\Grand Theft Auto V", "Grand Theft Auto V")]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike 2", "Counter-Strike 2")]
    [InlineData(@"D:\Epic Games\Fortnite", "Fortnite")]
    [InlineData(@"E:\Riot Games\League of Legends", "League of Legends")]
    public void Describe_GamesOnAnyDrive(string path, string game)
    {
        var description = LocationGuide.Describe(Folder(path));

        Assert.Equal(LocationKind.Applications, description.Kind);
        Assert.Contains(game, description.Summary);
    }

    [Theory]
    [InlineData(@"H:\projects\site\node_modules")]
    [InlineData(@"H:\a\b\c\d\node_modules")]
    public void Describe_DevelopmentFoldersAtAnyDepth(string path)
    {
        var description = LocationGuide.Describe(Folder(path));

        Assert.Contains("npm install", description.Advice);
    }

    [Fact]
    public void Describe_RecycleBinOfAnotherDrive()
    {
        Assert.Equal(RiskLevel.Safe, LocationGuide.Describe(Folder(@"D:\$RECYCLE.BIN")).Risk);
    }

    [Fact]
    public void Describe_KnownSystemFile()
    {
        var description = LocationGuide.Describe(new StorageUsage("pagefile.sys", 100, @"C:\pagefile.sys"));

        Assert.Equal(LocationKind.SystemFile, description.Kind);
        Assert.Equal(RiskLevel.Expert, description.Risk);
    }

    [Fact]
    public void Describe_FileByExtension()
    {
        var description = LocationGuide.Describe(new StorageUsage("setup.iso", 100, @"D:\Downloads\setup.iso"));

        Assert.Contains("image de disque", description.Summary);
    }

    [Fact]
    public void Describe_UnknownFolder_AsksTheUserToCheck()
    {
        var description = LocationGuide.Describe(Folder(@"C:\MyGames"));

        Assert.Equal(LocationKind.Folder, description.Kind);
        Assert.Equal(RiskLevel.Caution, description.Risk);
    }

    [Fact]
    public void Describe_UnknownFile()
    {
        var description = LocationGuide.Describe(new StorageUsage("notes.xyz", 100, @"C:\notes.xyz"));

        Assert.Equal(LocationKind.File, description.Kind);
    }

    [Fact]
    public void Describe_GroupedBucket_HasNoPath()
    {
        var description = LocationGuide.Describe(new StorageUsage("Autres (3 éléments)", 100));

        Assert.Equal(LocationKind.Group, description.Kind);
    }

    private static StorageUsage Folder(string path) =>
        new(Path.GetFileName(path.TrimEnd('\\')), 100, path, IsDirectory: true);
}
