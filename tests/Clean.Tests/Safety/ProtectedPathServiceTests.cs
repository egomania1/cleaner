using Clean.Core.Safety;

namespace Clean.Tests.Safety;

public class ProtectedPathServiceTests
{
    private static readonly ProtectedPathService Service = new(path => path
        .Replace("%SystemDrive%", "C:")
        .Replace("%USERPROFILE%", @"C:\Users\Test")
        .Replace("%LOCALAPPDATA%", @"C:\Users\Test\AppData\Local"));

    [Theory]
    [InlineData(@"C:\", @"C:\")]
    [InlineData("C:", @"C:\")]
    [InlineData(@"C:\Users", @"C:\Users")]
    [InlineData(@"C:\Users\Test\", @"C:\Users\Test")]
    [InlineData(@"c:\users\test\appdata", @"C:\Users\Test\AppData\Local")]
    [InlineData(@"C:\Users\Test\Documents", @"C:\Users\Test\Documents")]
    public void FindProtectedFolderWithin_FindsProtectedFoldersAndTheirParents(string path, string expected)
    {
        Assert.Equal(expected, Service.FindProtectedFolderWithin(path), ignoreCase: true);
    }

    [Theory]
    [InlineData(@"C:\Users\Test\AppData\Local\Temp")]
    [InlineData(@"C:\Users\Test\Documents\Old")]
    [InlineData(@"D:\Games\Cache")]
    public void FindProtectedFolderWithin_AllowsKnownSubfolders(string path)
    {
        Assert.Null(Service.FindProtectedFolderWithin(path));
    }
}
