using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Storage;

public class LocationGuideTests
{
    [Theory]
    [InlineData("Windows", RiskLevel.Blocked)]
    [InlineData("windows", RiskLevel.Blocked)]
    [InlineData("Program Files", RiskLevel.Blocked)]
    [InlineData("Users", RiskLevel.Caution)]
    [InlineData("$Recycle.Bin", RiskLevel.Safe)]
    public void Describe_KnownFolders(string name, RiskLevel expectedRisk)
    {
        var description = LocationGuide.Describe(new StorageUsage(name, 100, $@"C:\{name}", IsDirectory: true));

        Assert.Equal(expectedRisk, description.Risk);
    }

    [Fact]
    public void Describe_KnownSystemFile()
    {
        var description = LocationGuide.Describe(new StorageUsage("pagefile.sys", 100, @"C:\pagefile.sys"));

        Assert.Equal(LocationKind.SystemFile, description.Kind);
        Assert.Equal(RiskLevel.Expert, description.Risk);
    }

    [Fact]
    public void Describe_UnknownFolder_AsksTheUserToCheck()
    {
        var description = LocationGuide.Describe(new StorageUsage("MyGames", 100, @"C:\MyGames", IsDirectory: true));

        Assert.Equal(LocationKind.Folder, description.Kind);
        Assert.Equal(RiskLevel.Caution, description.Risk);
    }

    [Fact]
    public void Describe_UnknownFile()
    {
        var description = LocationGuide.Describe(new StorageUsage("notes.txt", 100, @"C:\notes.txt"));

        Assert.Equal(LocationKind.File, description.Kind);
    }

    [Fact]
    public void Describe_GroupedBucket_HasNoPath()
    {
        var description = LocationGuide.Describe(new StorageUsage("Autres (3 éléments)", 100));

        Assert.Equal(LocationKind.Group, description.Kind);
    }
}
