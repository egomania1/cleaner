using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Storage;

public class ProgramMatcherTests
{
    private static readonly ProgramInfo Chrome = new("Google Chrome", "Google LLC", "129.0", null, @"C:\Program Files\Google\Chrome\Application");
    private static readonly ProgramInfo Updater = new("Google Updater", "Google LLC", "1.0", null, @"C:\Program Files\Google\GoogleUpdater\");
    private static readonly ProgramInfo Steam = new("Steam", "Valve Corporation", null, null, "\"C:\\Program Files (x86)\\Steam\"");
    private static readonly ProgramInfo NoLocation = new("Mystery", null, null, null, null);

    private static readonly ProgramInfo[] Programs = [Chrome, Updater, Steam, NoLocation];

    [Fact]
    public void ProgramsAt_VendorFolder_ListsTheProgramsInstalledInside()
    {
        var programs = ProgramMatcher.ProgramsAt(Programs, @"C:\Program Files\Google");

        Assert.Equal(["Google Chrome", "Google Updater"], programs.Select(program => program.Name));
    }

    [Fact]
    public void ProgramsAt_SubfolderOfAnApplication_FindsThatApplication()
    {
        var programs = ProgramMatcher.ProgramsAt(Programs, @"C:\Program Files (x86)\Steam\steamapps");

        Assert.Equal("Steam", Assert.Single(programs).Name);
    }

    [Fact]
    public void ProgramsAt_IgnoresCaseQuotesAndTrailingSlashes()
    {
        var programs = ProgramMatcher.ProgramsAt(Programs, @"c:\program files (x86)\steam\");

        Assert.Equal("Steam", Assert.Single(programs).Name);
    }

    [Fact]
    public void ProgramsAt_UnrelatedFolder_FindsNothing()
    {
        Assert.Empty(ProgramMatcher.ProgramsAt(Programs, @"C:\Games"));
    }

    [Fact]
    public void ProgramsAt_DoesNotConfuseFoldersSharingAPrefix()
    {
        Assert.Empty(ProgramMatcher.ProgramsAt(Programs, @"C:\Program Files (x86)\SteamLibrary"));
    }
}
