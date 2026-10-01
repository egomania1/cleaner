using Clean.Core.Logging;
using Clean.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Clean.Tests.Logging;

public sealed class LogTests : IDisposable
{
    private static readonly LogSanitizer Sanitizer = new(@"C:\Users\Marie", "Marie");

    private readonly TestDirectory _folder = new();

    public void Dispose() => _folder.Dispose();

    [Theory]
    [InlineData(@"Could not read C:\Users\Marie\Documents\a.txt", @"Could not read %USERPROFILE%\Documents\a.txt")]
    [InlineData(@"could not read c:\users\marie\documents\a.txt", @"could not read %USERPROFILE%\documents\a.txt")]
    [InlineData(@"D:\Marie Games\save.dat", @"D:\<user> Games\save.dat")]
    [InlineData("Nothing personal here", "Nothing personal here")]
    public void Clean_HidesTheProfileFolderAndTheUserName(string text, string expected)
    {
        Assert.Equal(expected, Sanitizer.Clean(text));
    }

    [Fact]
    public void Clean_IgnoresNamesTooShortToBeSafelyMatched()
    {
        var sanitizer = new LogSanitizer(@"C:\Users\Al", "Al");

        Assert.Equal(@"Always %USERPROFILE%\x", sanitizer.Clean(@"Always C:\Users\Al\x"));
    }

    [Fact]
    public void Log_WritesOneSanitizedLineWithLevelAndCategory()
    {
        var logger = Provider().CreateLogger("Cleaner");

        logger.LogWarning("Could not clean {Path}", @"C:\Users\Marie\AppData\Local\Temp");

        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(_folder.RootPath)));
        Assert.Contains("[WRN] Cleaner: Could not clean %USERPROFILE%", text);
        Assert.DoesNotContain("Marie", text);
    }

    [Fact]
    public void Log_WritesTheSanitizedException()
    {
        var logger = Provider().CreateLogger("Cleaner");

        logger.LogError(new IOException(@"C:\Users\Marie\x is locked"), "Failed");

        var text = File.ReadAllText(Assert.Single(Directory.GetFiles(_folder.RootPath)));
        Assert.Contains("%USERPROFILE%", text);
        Assert.DoesNotContain("Marie", text);
    }

    [Fact]
    public void Log_IgnoresLevelsBelowTheMinimum()
    {
        var logger = Provider().CreateLogger("Cleaner");

        logger.LogDebug("noise");

        Assert.Empty(Directory.GetFiles(_folder.RootPath));
    }

    [Fact]
    public void Log_StopsGrowingAFileAtTheMaximumSize()
    {
        var logger = Provider().CreateLogger("Cleaner");
        var big = new string('x', 100_000);

        for (var i = 0; i < 40; i++)
        {
            logger.LogInformation("{Text}", big);
        }

        var file = new FileInfo(Assert.Single(Directory.GetFiles(_folder.RootPath)));
        Assert.InRange(file.Length, FileLoggerProvider.MaximumFileBytes, FileLoggerProvider.MaximumFileBytes + 200_000);
    }

    [Fact]
    public void Constructor_DeletesLogsOlderThanTheRetentionOnly()
    {
        var old = _folder.CreateFile("clean-20200101.log", 10);
        var recent = _folder.CreateFile("clean-20990101.log", 10);
        var other = _folder.CreateFile("notes.txt", 10);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow - FileLoggerProvider.Retention - TimeSpan.FromDays(1));
        File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddYears(-5));

        _ = Provider();

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Log_NeverThrowsWhenTheFolderIsUnusable()
    {
        var blocked = _folder.CreateFile("blocked", 1);
        var logger = new FileLoggerProvider(Path.Combine(blocked, "logs"), Sanitizer, TimeProvider.System).CreateLogger("Cleaner");

        logger.LogError("still fine");
    }

    private FileLoggerProvider Provider() => new(_folder.RootPath, Sanitizer, TimeProvider.System);
}
