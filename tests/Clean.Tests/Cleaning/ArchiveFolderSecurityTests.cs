using System.Security.AccessControl;
using System.Security.Principal;
using Clean.Infrastructure.Cleaning;

namespace Clean.Tests.Cleaning;

public sealed class ArchiveFolderSecurityTests : IDisposable
{
    private readonly TestDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Protect_LimitsTheFolderToTheUserTheSystemAndTheAdministrators()
    {
        var folder = new DirectoryInfo(_temp.CreateDirectory("archive"));

        Assert.True(ArchiveFolderSecurity.Protect(folder));

        var security = folder.GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        var identities = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Select(rule => rule.IdentityReference.Value)
            .ToHashSet();
        Assert.Equal(
            new HashSet<string>
            {
                WindowsIdentity.GetCurrent().User!.Value,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value,
            },
            identities);
    }

    [Fact]
    public void Protect_KeepsTheFolderUsableByTheCurrentUser()
    {
        var folder = new DirectoryInfo(_temp.CreateDirectory("archive"));
        ArchiveFolderSecurity.Protect(folder);

        var file = Path.Combine(folder.FullName, "sessions", "a.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, [1, 2, 3]);

        Assert.Equal([1, 2, 3], File.ReadAllBytes(file));
    }

    [Fact]
    public void Protect_AppliesToFilesAlreadyInsideTheFolder()
    {
        var folder = new DirectoryInfo(_temp.CreateDirectory("archive"));
        var file = new FileInfo(_temp.CreateFile(@"archive\old.bin", 5));

        ArchiveFolderSecurity.Protect(folder);

        Assert.Equal(3, file.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Count);
    }

    [Fact]
    public void Protect_ReportsFailureInsteadOfThrowingWhenTheFolderDoesNotExist()
    {
        Assert.False(ArchiveFolderSecurity.Protect(new DirectoryInfo(Path.Combine(_temp.RootPath, "missing"))));
    }
}
