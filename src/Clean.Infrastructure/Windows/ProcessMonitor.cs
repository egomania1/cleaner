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

    private readonly Dictionary<(int, DateTime?), (string? Path, string? Description)> _identities = [];

    public int ProcessorCount => Environment.ProcessorCount;

    public long TotalMemoryBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public IReadOnlyList<ProcessSample> Sample()
    {
        var samples = new List<ProcessSample>();
        var alive = new HashSet<(int, DateTime?)>();

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
                    Try(() => process.WorkingSet64)));
            }
        }

        foreach (var gone in _identities.Keys.Where(key => !alive.Contains(key)).ToList())
        {
            _identities.Remove(gone);
        }

        return samples;
    }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
