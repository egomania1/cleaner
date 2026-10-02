using Clean.Core.Models;
using Clean.Core.Uninstall;

namespace Clean.Tests.Uninstall;

public sealed class UninstallCommandTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Foo\\unins000.exe\" /SILENT", "C:\\Program Files\\Foo\\unins000.exe", "/SILENT")]
    [InlineData("C:\\Program Files\\Foo Bar\\uninstall.exe /S", "C:\\Program Files\\Foo Bar\\uninstall.exe", "/S")]
    [InlineData("MsiExec.exe /X{ABC-123}", "MsiExec.exe", "/X{ABC-123}")]
    [InlineData("  \"C:\\x\\u.exe\"  ", "C:\\x\\u.exe", "")]
    [InlineData("C:\\Program Files\\Foo.exeutils\\remove.exe -q", "C:\\Program Files\\Foo.exeutils\\remove.exe", "-q")]
    public void Parse_SplitsTheProgramFromItsArguments(string text, string fileName, string arguments)
    {
        var command = UninstallCommand.Parse(text);

        Assert.NotNull(command);
        Assert.Equal(fileName, command.FileName);
        Assert.Equal(arguments, command.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("\"C:\\x\\script.bat\"")]
    [InlineData("cmd /c del everything")]
    [InlineData("C:\\x\\readme.txt")]
    public void Parse_RefusesAnythingThatIsNotAnExecutable(string? text)
    {
        Assert.Null(UninstallCommand.Parse(text));
    }
}

public sealed class LeftoverNamesTests
{
    [Fact]
    public void For_ListsTheNameTheNameWithoutVersionAndTheInstallFolder()
    {
        var program = new ProgramInfo("Foo Studio 2.1.0 (x64)", null, null, null, @"C:\Games\FooStudio");

        var names = LeftoverNames.For(program);

        Assert.Contains("Foo Studio 2.1.0 (x64)", names);
        Assert.Contains("Foo Studio", names);
        Assert.Contains("FooStudio", names);
    }

    [Fact]
    public void For_DropsNamesTooShortToBeTrusted()
    {
        var program = new ProgramInfo("7z", null, null, null, @"C:\Tools\7z");

        Assert.Empty(LeftoverNames.For(program));
    }

    [Fact]
    public void For_DropsNamesThatCouldNotBeAFolder()
    {
        var program = new ProgramInfo("Foo: Bar/Baz", null, null, null, null);

        Assert.Empty(LeftoverNames.For(program));
    }
}
