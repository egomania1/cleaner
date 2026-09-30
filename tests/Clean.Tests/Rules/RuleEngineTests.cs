using Clean.Core.Models;
using Clean.Core.Rules;

namespace Clean.Tests.Rules;

public class RuleEngineTests
{
    private static readonly RuleEngine Engine = new(
        [
            Rule("NVIDIA", @"%LOCALAPPDATA%\NVIDIA"),
            Rule("NVIDIA_DX", @"%LOCALAPPDATA%\NVIDIA\DXCache"),
            Rule("MISSING", @"%NOT_DEFINED%\Cache"),
        ],
        path => path.Replace("%LOCALAPPDATA%", @"C:\Users\Test\AppData\Local"));

    [Theory]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\DXCache", "NVIDIA_DX")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA\DXCache\shader.bin", "NVIDIA_DX")]
    [InlineData(@"c:\users\test\appdata\local\nvidia\glcache\a.bin", "NVIDIA")]
    public void FindRuleFor_ReturnsTheDeepestMatchingRule(string path, string expectedId)
    {
        Assert.Equal(expectedId, Engine.FindRuleFor(path)?.Id);
    }

    [Theory]
    [InlineData(@"C:\Users\Test\AppData\Local")]
    [InlineData(@"C:\Users\Test\AppData\Local\NVIDIA-Other\file.bin")]
    [InlineData(@"D:\Cache")]
    public void FindRuleFor_ReturnsNullOutsideRuleFolders(string path)
    {
        Assert.Null(Engine.FindRuleFor(path));
    }

    private static CleaningRule Rule(string id, string path) =>
        new(id, id, "Test.", CleaningCategory.GpuCache, RiskLevel.Safe, [path], 0, true);
}
