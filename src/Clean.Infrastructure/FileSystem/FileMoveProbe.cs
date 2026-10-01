using System.Runtime.InteropServices;
using Clean.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace Clean.Infrastructure.FileSystem;

// Asks Windows for the one right a move needs (DELETE) without reading the file: it answers exactly
// "could this file be moved now", and unlike a read it does not make the antivirus scan every file.
internal static class FileMoveProbe
{
    private const uint Delete = 0x00010000;
    private const uint ShareAll = 0x7;
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const int AccessDenied = 5;
    private const int SharingViolation = 32;
    private const int LockViolation = 33;

    public static KeptFileReason? FindReasonItCannotMove(string path)
    {
        using var handle = CreateFile(path, Delete, ShareAll, IntPtr.Zero, OpenExisting, OpenReparsePoint, IntPtr.Zero);
        if (!handle.IsInvalid)
        {
            return null;
        }

        return Marshal.GetLastWin32Error() switch
        {
            SharingViolation or LockViolation => KeptFileReason.InUse,
            AccessDenied => KeptFileReason.Inaccessible,
            _ => null,
        };
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
