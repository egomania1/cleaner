using Clean.Core.Startup;
using Clean.Infrastructure.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace Clean.Tests.Windows;

// The registry tests run in a throwaway key under HKCU\Software\Clean.Tests: nothing real is read or changed.
public sealed class StartupTests : IDisposable
{
    private const string Approval = "Approved";

    private readonly string _keyPath = $@"Software\Clean.Tests\{Guid.NewGuid():N}";
    private readonly RegistryKey _root;
    private readonly RegistryKey _machineHive;
    private readonly TestDirectory _folders = new();

    public StartupTests()
    {
        _root = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
        _machineHive = _root.CreateSubKey("Machine", writable: true);
    }

    public void Dispose()
    {
        _machineHive.Dispose();
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        _folders.Dispose();
    }

    [Theory]
    [InlineData(@"""C:\Program Files\App\app.exe"" --minimized", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\App\app.exe /silent", @"C:\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe /x", @"C:\Program Files\App\app.exe")]
    [InlineData(@"""C:\Users\Test\AppData\Roaming\Spotify\Spotify.exe"" --autostart", @"C:\Users\Test\AppData\Roaming\Spotify\Spotify.exe")]
    [InlineData(@"C:\tools\run.cmd arg", @"C:\tools\run.cmd")]
    [InlineData(@"C:\tools\run.cmd", @"C:\tools\run.cmd")]
    public void ExtractExecutable_FindsTheProgramOfACommandLine(string command, string expected)
    {
        Assert.Equal(expected, StartupCommand.ExtractExecutable(command, text => text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"""unterminated")]
    public void ExtractExecutable_ReturnsNothingForAnUnusableCommand(string? command)
    {
        Assert.Null(StartupCommand.ExtractExecutable(command, text => text));
    }

    [Fact]
    public void ExtractExecutable_ExpandsEnvironmentVariables()
    {
        Assert.Equal(@"C:\Roaming\App\app.exe", StartupCommand.ExtractExecutable(@"%APPDATA%\App\app.exe --x", text => text.Replace("%APPDATA%", @"C:\Roaming")));
    }

    [Fact]
    public void Read_ListsRegistryEntriesWithTheirProgramAndEnabledState()
    {
        var program = Environment.ProcessPath!;
        Run().SetValue("My App", $"\"{program}\" --minimized");
        Run().SetValue("Ghost", @"""C:\Definitely\Missing\ghost.exe""");

        var entries = Catalog().Read();

        var app = Assert.Single(entries, entry => entry.Name == "My App");
        Assert.Equal(program, app.ExecutablePath);
        Assert.True(app.IsEnabled);
        Assert.False(app.TargetMissing);
        Assert.Equal(StartupSource.UserRegistry, app.Source);
        Assert.True(Assert.Single(entries, entry => entry.Name == "Ghost").TargetMissing);
    }

    [Fact]
    public void Read_IgnoresValuesThatAreNotCommands()
    {
        Run().SetValue("Number", 5, RegistryValueKind.DWord);
        Run().SetValue("Empty", "");
        Run().SetValue("", "default value");
        Run().SetValue("Real", @"C:\real.exe");

        Assert.Equal(["Real"], Catalog().Read().Select(entry => entry.Name));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(6, true)]
    [InlineData(3, false)]
    [InlineData(1, false)]
    public void Read_FollowsTheStartupApprovedMark(byte state, bool enabled)
    {
        Run().SetValue("App", @"C:\app.exe");
        Mark(_root, "Run", "App", state);

        Assert.Equal(enabled, Assert.Single(Catalog().Read()).IsEnabled);
    }

    [Fact]
    public void Read_AMachineMarkTurnsAnEntryOffToo()
    {
        Run().SetValue("App", @"C:\app.exe");
        Mark(_machineHive, "Run", "App", 3);

        Assert.False(Assert.Single(Catalog().Read()).IsEnabled);
    }

    [Fact]
    public void Read_ListsStartupFolderFilesButNotDesktopIni()
    {
        _folders.CreateFile(@"user\Tool.lnk", 10);
        _folders.CreateFile(@"user\desktop.ini", 10);
        var folder = new StartupLocation(StartupSource.UserFolder, null, null, Path.Combine(_folders.RootPath, "user"));

        var entry = Assert.Single(Catalog(folder).Read());

        Assert.Equal("Tool.lnk", entry.Name);
        Assert.Equal(StartupSource.UserFolder, entry.Source);
    }

    [Fact]
    public void Read_SurvivesMissingKeysAndFolders()
    {
        var missing = new[]
        {
            new StartupLocation(StartupSource.UserRegistry, _root, "NoSuchKey", null),
            new StartupLocation(StartupSource.UserFolder, null, null, Path.Combine(_folders.RootPath, "nowhere")),
        };

        Assert.Empty(new StartupCatalog(missing, _root, _machineHive, Approval, NullLogger<StartupCatalog>.Instance).Read());
    }

    [Fact]
    public void SetEnabled_TurnsAnEntryOffAndBackOnWithoutDeletingIt()
    {
        Run().SetValue("App", @"C:\app.exe");
        var catalog = Catalog();
        var id = Assert.Single(catalog.Read()).Id;

        Assert.Equal(StartupChangeResult.Changed, catalog.SetEnabled(id, false));
        var off = Assert.Single(catalog.Read());
        Assert.False(off.IsEnabled);
        Assert.Equal(@"C:\app.exe", Run().GetValue("App"));
        Assert.Equal(3, ((byte[])_root.OpenSubKey($@"{Approval}\Run")!.GetValue("App")!)[0]);
        Assert.Equal(12, ((byte[])_root.OpenSubKey($@"{Approval}\Run")!.GetValue("App")!).Length);

        Assert.Equal(StartupChangeResult.Changed, catalog.SetEnabled(id, true));
        Assert.True(Assert.Single(catalog.Read()).IsEnabled);
        Assert.Equal(2, ((byte[])_root.OpenSubKey($@"{Approval}\Run")!.GetValue("App")!)[0]);
    }

    [Fact]
    public void SetEnabled_NeverTouchesAnEntryOfAllUsers()
    {
        Machine().SetValue("Shared", @"C:\shared.exe");
        var catalog = Catalog();
        var id = Assert.Single(catalog.Read()).Id;

        Assert.Equal(StartupChangeResult.NeedsAdministrator, catalog.SetEnabled(id, false));
        Assert.Null(_root.OpenSubKey($@"{Approval}\Run"));
    }

    [Theory]
    [InlineData("UserRegistry|DoesNotExist")]
    [InlineData("MachineRegistry|App")]
    [InlineData("UserRegistry|")]
    [InlineData("..\\..\\Anything")]
    [InlineData("")]
    public void SetEnabled_OnlyChangesWhatWasListed(string id)
    {
        Run().SetValue("App", @"C:\app.exe");

        Assert.Equal(StartupChangeResult.NotFound, Catalog().SetEnabled(id, false));
        Assert.Null(_root.OpenSubKey($@"{Approval}\Run"));
    }

    [Fact]
    public void SetEnabled_UsesTheStartupFolderGroupForFolderEntries()
    {
        _folders.CreateFile(@"user\Tool.lnk", 10);
        var folder = new StartupLocation(StartupSource.UserFolder, null, null, Path.Combine(_folders.RootPath, "user"));
        var catalog = Catalog(folder);

        Assert.Equal(StartupChangeResult.Changed, catalog.SetEnabled(Assert.Single(catalog.Read()).Id, false));

        Assert.Equal(3, ((byte[])_root.OpenSubKey($@"{Approval}\StartupFolder")!.GetValue("Tool.lnk")!)[0]);
        Assert.False(Assert.Single(catalog.Read()).IsEnabled);
    }

    private RegistryKey Run() => _root.CreateSubKey("Run", writable: true);

    private RegistryKey Machine() => _root.CreateSubKey("MachineRun", writable: true);

    private static void Mark(RegistryKey hive, string group, string name, byte state)
    {
        using var key = hive.CreateSubKey($@"{Approval}\{group}", writable: true);
        var value = new byte[12];
        value[0] = state;
        key.SetValue(name, value, RegistryValueKind.Binary);
    }

    private StartupCatalog Catalog(params StartupLocation[] extra) => new(
        [
            new StartupLocation(StartupSource.UserRegistry, _root, "Run", null),
            new StartupLocation(StartupSource.MachineRegistry, _root, "MachineRun", null),
            .. extra,
        ],
        _root,
        _machineHive,
        Approval,
        NullLogger<StartupCatalog>.Instance);
}
