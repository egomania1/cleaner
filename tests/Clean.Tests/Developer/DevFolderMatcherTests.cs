using Clean.Core.Developer;

namespace Clean.Tests.Developer;

public sealed class DevFolderMatcherTests
{
    [Theory]
    [InlineData("node_modules", "package.json", "NODE_MODULES")]
    [InlineData("bin", "App.csproj", "DOTNET_BIN")]
    [InlineData("obj", "App.csproj", "DOTNET_OBJ")]
    [InlineData("target", "Cargo.toml", "CARGO_TARGET")]
    [InlineData("target", "pom.xml", "MAVEN_TARGET")]
    [InlineData("build", "build.gradle.kts", "GRADLE_BUILD_KTS")]
    [InlineData("__pycache__", "anything.py", "PYCACHE")]
    [InlineData("NODE_MODULES", "PACKAGE.JSON", "NODE_MODULES")]
    public void Match_RecognisesAFolderOnlyNextToItsProjectFile(string folder, string sibling, string expectedId)
    {
        Assert.Equal(expectedId, DevFolderMatcher.Match(folder, [sibling])?.Id);
    }

    [Theory]
    [InlineData("node_modules", "readme.md")]
    [InlineData("bin", "notes.txt")]
    [InlineData("target", "readme.md")]
    [InlineData("build", "build.xml")]
    [InlineData("Documents", "package.json")]
    public void Match_IgnoresAFolderWithoutTheProjectFileThatExplainsIt(string folder, string sibling)
    {
        Assert.Null(DevFolderMatcher.Match(folder, [sibling]));
    }

    [Fact]
    public void Match_NeverMatchesWithoutAnySibling()
    {
        Assert.Null(DevFolderMatcher.Match("node_modules", []));
    }

    [Fact]
    public void AllKinds_HaveTheTextsTheUserNeeds()
    {
        Assert.All(DevFolderKind.All, kind =>
        {
            Assert.False(string.IsNullOrWhiteSpace(kind.Title));
            Assert.False(string.IsNullOrWhiteSpace(kind.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(kind.HowToRestore));
        });
        Assert.Equal(DevFolderKind.All.Count, DevFolderKind.All.Select(kind => kind.Id).Distinct().Count());
    }
}
