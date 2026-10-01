using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Clean.Infrastructure.Licensing;

// A stable id for this PC that cannot be turned back into the Windows machine id: it is hashed with a
// product-specific salt, so the licence server and the app never see the raw identifier.
public static class DeviceIdentity
{
    private const string Salt = "Clean.Device.v1:";

    public static string Current()
    {
        var machineId = ReadMachineId() ?? Environment.MachineName;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Salt + machineId)))[..32].ToLowerInvariant();
    }

    private static string? ReadMachineId()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid") as string;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
