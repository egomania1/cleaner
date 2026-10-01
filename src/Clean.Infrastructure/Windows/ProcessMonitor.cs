using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Infrastructure.Windows;

public sealed class ProcessMonitor : IProcessMonitor
{
    // Enough to read the path of most processes, including other users' ones, without administrator rights.
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private const uint GetWindowOwner = 4;
    private const int ExtendedStyleIndex = -20;
    private const long WindowExToolWindow = 0x80;
    private const long WindowExAppWindow = 0x40000;
    private const int DwmCloaked = 14;

    private readonly Dictionary<(int, DateTime?), (string? Path, string? Description)> _identities = [];

    public int ProcessorCount => Environment.ProcessorCount;

    public long TotalMemoryBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public IReadOnlyList<ProcessSample> Sample()
    {
        var samples = new List<ProcessSample>();
        var alive = new HashSet<(int, DateTime?)>();
        var windowOwners = VisibleWindowOwners();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                // Id 0 is the "System Idle Process": its "CPU time" is the time nobody used.
                if (process.Id == 0)
                {
                    continue;
                }

                var start = Try<DateTime?>(() => process.StartTime);
                var key = (process.Id, start);
                alive.Add(key);

                if (!_identities.TryGetValue(key, out var identity))
                {
                    var path = ExecutablePath(process.Id);
                    identity = (path, path is null ? null : Try(() => FileVersionInfo.GetVersionInfo(path).FileDescription));
                    _identities[key] = identity;
                }

                samples.Add(new ProcessSample(
                    process.Id,
                    start,
                    process.ProcessName,
                    identity.Path,
                    identity.Description,
                    Try<TimeSpan?>(() => process.TotalProcessorTime),
                    Try(() => process.WorkingSet64),
                    windowOwners.Contains(process.Id)));
            }
        }

        foreach (var gone in _identities.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            _identities.Remove(gone);
        }

        return samples;
    }

    // The same idea as Task Manager's "Apps": a top-level window that is shown, has a title and is not
    // a tool window, an owned dialog or a suspended app that Windows keeps hidden ("cloaked").
    private static HashSet<int> VisibleWindowOwners()
    {
        var owners = new HashSet<int>();

        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window)
                && GetWindow(window, GetWindowOwner) == IntPtr.Zero
                && GetWindowTextLength(window) > 0
                && !IsToolWindow(window)
                && !IsCloaked(window))
            {
                GetWindowThreadProcessId(window, out var processId);
                owners.Add((int)processId);
            }

            return true;
        }, IntPtr.Zero);

        return owners;
    }

    private static bool IsToolWindow(IntPtr window)
    {
        var style = GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        return (style & WindowExToolWindow) != 0 && (style & WindowExAppWindow) == 0;
    }

    private static bool IsCloaked(IntPtr window) =>
        DwmGetWindowAttribute(window, DwmCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static string? ExecutablePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    // Protected and exiting processes refuse some reads; the value is simply unknown for them.
    private static T? Try<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
