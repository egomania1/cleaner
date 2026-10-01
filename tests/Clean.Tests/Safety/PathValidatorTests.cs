using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Rules;
using Clean.Core.Safety;

namespace Clean.Tests.Safety;

public class PathValidatorTests
{
    private const string CacheFolder = @"C:\Users\Test\AppData\Local\NVIDIA\DXCache";

    [Fact]
    public void Validate_AcceptsTheExactRuleFolderAndReturnsItsCanonicalForm()
    {
        var result = Validator().Validate(CacheFolder + @"\");

        Assert.True(result.IsValid);
        Assert.Equal(CacheFolder, result.CanonicalPath);
    }

    [Theory]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\DXCache\..\DXCache")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\.\DXCache")]
    [InlineData(@"\server\share\DXCache")]
    [InlineData(@"\?\C:\Users\Test\AppData\Local\NVIDIA\DXCache")]
    [InlineData(@"C:/Users/Test/AppData/Local/NVIDIA/DXCache")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\DXCache:hidden")]
    [InlineData(@"C:\")]
    [InlineData(@"D:\")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\DXCache\Sub")]
    public void Validate_RejectsTraversalDevicePathsRootsAndOtherFolders(string path)
    {
        Assert.False(Validator().Validate(path).IsValid);
    }

    [Theory]
    [InlineData(@"%USERPROFILE%")]
    [InlineData(@"%USERPROFILE%\Documents")]
    [InlineData(@"%WINDIR%")]
    public void Validate_RejectsProtectedFoldersEvenWhenARuleNamesThem(string rulePath)
    {
        var validator = Validator(Rule(rulePath));

        Assert.False(validator.Validate(Expand(rulePath)).IsValid);
    }

    [Theory]
    [InlineData(CacheFolder)]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA")]
    public void Validate_RejectsARuleFolderWithALinkOnTheWay(string link)
    {
        var result = Validator(detector: new LinkAt(link)).Validate(CacheFolder);

        Assert.False(result.IsValid);
        Assert.Contains(link, result.Reason);
    }

    private static PathValidator Validator(CleaningRule? rule = null, IReparsePointDetector? detector = null) =>
        new(new RuleEngine([rule ?? Rule(@"%LOCALAPPDATA%\NVIDIA\DXCache")], Expand), detector ?? new NoLinks(), Expand);

    private static CleaningRule Rule(string path) =>
        new("NVIDIA_DX", "Cache", "Rebuilt.", CleaningCategory.GpuCache, RiskLevel.Safe, [path], 0, true);

    private static string Expand(string path) => path
        .Replace("%LOCALAPPDATA%", @"C:\Users\Test\AppData\Local")
        .Replace("%USERPROFILE%", @"C:\Users\Test")
        .Replace("%WINDIR%", @"C:\Windows")
        .Replace("%SystemDrive%", "C:");
}
