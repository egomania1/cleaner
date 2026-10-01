using Clean.Core.Models;
using Clean.Core.Scanning;

namespace Clean.Tests.Scanning;

public class TempFilePolicyTests
{
    private static readonly DateTime Cutoff = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Old = Cutoff.AddDays(-10);

    [Theory]
    [InlineData("setup.log")]
    [InlineData("chrome_installer.exe")]
    [InlineData("~DF1234.tmp")]
    [InlineData("no-extension")]
    public void FindReasonToKeep_LetsOldLeftoversGo(string name)
    {
        Assert.Null(TempFilePolicy.FindReasonToKeep(name, FileAttributes.Normal, Old, Cutoff));
    }

    [Theory]
    [InlineData("contrat.pdf")]
    [InlineData("Rapport.DOCX")]
    [InlineData("photo.jpg")]
    [InlineData("vacances.mp4")]
    [InlineData("chanson.mp3")]
    public void FindReasonToKeep_KeepsWhatLooksLikeTheUsersOwnFiles(string name)
    {
        Assert.Equal(KeptFileReason.PersonalFile, TempFilePolicy.FindReasonToKeep(name, FileAttributes.Normal, Old, Cutoff));
    }

    [Fact]
    public void FindReasonToKeep_KeepsRecentFiles()
    {
        Assert.Equal(KeptFileReason.TooRecent, TempFilePolicy.FindReasonToKeep("a.tmp", FileAttributes.Normal, Cutoff.AddHours(1), Cutoff));
    }

    [Fact]
    public void FindReasonToKeep_KeepsSystemFiles()
    {
        Assert.Equal(KeptFileReason.SystemFile, TempFilePolicy.FindReasonToKeep("a.tmp", FileAttributes.System, Old, Cutoff));
    }

    [Theory]
    [InlineData(FileAttributes.Offline)]
    [InlineData((FileAttributes)0x00400000)]
    public void FindReasonToKeep_KeepsCloudPlaceholders(FileAttributes attributes)
    {
        Assert.Equal(KeptFileReason.CloudFile, TempFilePolicy.FindReasonToKeep("a.tmp", attributes, Old, Cutoff));
    }
}
