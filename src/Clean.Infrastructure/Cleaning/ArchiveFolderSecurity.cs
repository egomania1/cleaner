using System.Security.AccessControl;
using System.Security.Principal;

namespace Clean.Infrastructure.Cleaning;

// The archive of a secondary drive sits at its root, where the default rights often let every account of the PC
// read it. It can hold personal files, so only the current user, the system and the administrators get in.
public static class ArchiveFolderSecurity
{
    private const InheritanceFlags InheritToEverythingBelow = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

    // Returns false when the drive cannot hold rights (FAT, exFAT) or they cannot be changed: the folder then stays as it was.
    public static bool Protect(DirectoryInfo folder)
    {
        try
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            SecurityIdentifier[] allowed =
            [
                WindowsIdentity.GetCurrent().User!,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            ];
            foreach (var sid in allowed)
            {
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritToEverythingBelow, PropagationFlags.None, AccessControlType.Allow));
            }

            folder.SetAccessControl(security);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or PlatformNotSupportedException or InvalidOperationException)
        {
            return false;
        }
    }
}
