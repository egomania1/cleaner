using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Clean.Core.Files;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Clean.Infrastructure.FileSystem;

// Same size, then same first and last 64 KB, then same full SHA-256: each step only reads the files
// the previous one could not tell apart, so most files are never read at all.
public sealed class DuplicateFinder(ILogger<DuplicateFinder> logger) : IDuplicateFinder
{
    private const int SampleBytes = 64 * 1024;
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

    public Task<IReadOnlyList<DuplicateGroup>> FindAsync(IReadOnlyList<FoundFile> files, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Find(files, progress, cancellationToken), cancellationToken);

    private IReadOnlyList<DuplicateGroup> Find(IReadOnlyList<FoundFile> files, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken)
    {
        var sameSize = files
            .Where(file => file.SizeBytes > 0)
            .GroupBy(file => file.SizeBytes)
            .Where(group => group.Count() > 1)
            .Select(group => DistinctFiles(group.ToList()))
            .Where(group => group.Count > 1)
            .ToList();

        var tracker = new Tracker(progress, sameSize.Sum(group => group.Sum(file => Math.Min(file.SizeBytes, 2L * SampleBytes))));
        var sameSample = Regroup(sameSize, file => Hash(file, sampleOnly: true, tracker, cancellationToken));

        // Files up to two samples long were read entirely already.
        var toHashFully = sameSample.Where(group => group[0].SizeBytes > 2L * SampleBytes).ToList();
        tracker.AddToCompare(toHashFully.Sum(group => group.Sum(file => file.SizeBytes)));
        var confirmed = Regroup(toHashFully, file => Hash(file, sampleOnly: false, tracker, cancellationToken))
            .Concat(sameSample.Where(group => group[0].SizeBytes <= 2L * SampleBytes))
            .ToList();

        tracker.Report(string.Empty);
        return confirmed
            .Select(group => new DuplicateGroup(
                Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(group[0].Path))),
                group[0].SizeBytes,
                group.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderByDescending(group => group.WastedBytes)
            .ToList();
    }

    private static List<List<FoundFile>> Regroup(IEnumerable<List<FoundFile>> groups, Func<FoundFile, string?> key) =>
        groups
            .SelectMany(group => group
                .Select(file => (File: file, Key: key(file)))
                .Where(entry => entry.Key is not null)
                .GroupBy(entry => entry.Key)
                .Select(sub => sub.Select(entry => entry.File).ToList()))
            .Where(group => group.Count > 1)
            .ToList();

    private string? Hash(FoundFile file, bool sampleOnly, Tracker tracker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.SequentialScan);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];

            if (sampleOnly)
            {
                ReadInto(stream, sha, Math.Min(SampleBytes, file.SizeBytes), buffer, tracker, file.Path, cancellationToken);
                if (file.SizeBytes > SampleBytes)
                {
                    stream.Seek(-Math.Min(SampleBytes, file.SizeBytes - SampleBytes), SeekOrigin.End);
                    ReadInto(stream, sha, Math.Min(SampleBytes, file.SizeBytes - SampleBytes), buffer, tracker, file.Path, cancellationToken);
                }
            }
            else
            {
                ReadInto(stream, sha, long.MaxValue, buffer, tracker, file.Path, cancellationToken);
            }

            return Convert.ToHexString(sha.GetHashAndReset());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read cannot be proven identical, so it is simply left out.
            logger.LogDebug(exception, "Could not read {File}", file.Path);
            return null;
        }
    }

    private static void ReadInto(FileStream stream, IncrementalHash sha, long count, byte[] buffer, Tracker tracker, string path, CancellationToken cancellationToken)
    {
        long remaining = count;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                break;
            }

            sha.AppendData(buffer, 0, read);
            remaining -= read;
            tracker.Add(read, path);
        }
    }

    // Hard links (used by pnpm, WinGet or Windows itself) are one file under two names: removing one
    // name frees nothing, so they are not duplicates.
    private List<FoundFile> DistinctFiles(List<FoundFile> files) =>
        files
            .GroupBy(file => FileIdentity(file.Path) ?? file.Path)
            .Select(group => group.First())
            .ToList();

    private string? FileIdentity(string path)
    {
        try
        {
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return GetFileInformationByHandle(handle, out var info)
                ? $"{info.VolumeSerialNumber:X8}-{info.FileIndexHigh:X8}{info.FileIndexLow:X8}"
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Could not identify {File}", path);
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    private sealed class Tracker(IProgress<DuplicateProgress>? progress, long toCompare)
    {
        private readonly System.Diagnostics.Stopwatch _stopwatch = System.Diagnostics.Stopwatch.StartNew();
        private TimeSpan _lastReport;
        private long _toCompare = toCompare;
        private long _compared;

        public void AddToCompare(long bytes) => _toCompare += bytes;

        public void Add(long bytes, string path)
        {
            _compared += bytes;
            if (_stopwatch.Elapsed - _lastReport >= ReportInterval)
            {
                Report(path);
            }
        }

        public void Report(string path)
        {
            _lastReport = _stopwatch.Elapsed;
            progress?.Report(new DuplicateProgress(_toCompare, Math.Min(_compared, _toCompare), path));
        }
    }
}
