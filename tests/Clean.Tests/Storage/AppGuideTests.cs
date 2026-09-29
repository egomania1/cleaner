using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Storage;

public class AppGuideTests
{
    [Theory]
    [InlineData("FiveM", "FiveM")]
    [InlineData("FiveM.app", "FiveM")]
    [InlineData("fiveml", "FiveM")]
    [InlineData("Google Chrome", "Google Chrome")]
    [InlineData("VALORANT", "VALORANT")]
    [InlineData("Riot Games", "Riot Games")]
    [InlineData("NVIDIA Corporation", "NVIDIA")]
    public void Find_RecognisesCommonApplications(string name, string expected)
    {
        Assert.Equal(expected, AppGuide.Find(name)?.Name);
    }

    [Theory]
    [InlineData("lolcats")]
    [InlineData("My Projects")]
    [InlineData("")]
    [InlineData(null)]
    public void Find_DoesNotGuess(string? name)
    {
        Assert.Null(AppGuide.Find(name));
    }

    [Fact]
    public void Describe_UnknownFolderNamedAfterAnApp_ExplainsTheApp()
    {
        var description = LocationGuide.Describe(new StorageUsage("fiveml", 100, @"F:\fiveml", IsDirectory: true));

        Assert.Equal(LocationKind.Applications, description.Kind);
        Assert.Contains("GTA V", description.Summary);
        Assert.Contains("cache", description.Advice);
    }

    [Fact]
    public void Describe_GameFolder_PrefersTheAppProfile()
    {
        var description = LocationGuide.Describe(new StorageUsage("VALORANT", 100, @"F:\Riot Games\VALORANT", IsDirectory: true));

        Assert.Contains("tir tactique", description.Summary);
    }

    [Fact]
    public void Describe_KnownExecutable()
    {
        var description = LocationGuide.Describe(new StorageUsage("Discord.exe", 100, @"C:\Apps\Discord.exe"));

        Assert.Contains("Discord", description.Summary);
    }

    [Fact]
    public void DescribeProgram_UnknownProgram_UsesItsRealNameAndPublisher()
    {
        var description = LocationGuide.DescribeProgram(new ProgramInfo("Super Tool", "Acme", "2.0", null, @"C:\Acme"));

        Assert.Contains("Super Tool", description.Summary);
        Assert.Contains("Acme", description.Summary);
    }
}
