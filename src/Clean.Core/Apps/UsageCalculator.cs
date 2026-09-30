using Clean.Core.Models;

namespace Clean.Core.Apps;

// Turns two process snapshots into per-application usage: processes are grouped under the installed
// program whose folder holds their executable, so Chrome's 30 processes read as one "Google Chrome".
public sealed class UsageCalculator
{
    public const string WindowsKey = "windows";
    public const string ProtectedKey = "protected";

    private readonly List<(ProgramInfo Program, string Location)> _locations;
    private readonly string _windowsFolder;
    private readonly Dictionary<string, ProgramInfo?> _programByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProgramInfo> _byName;

    public UsageCalculator(IEnumerable<ProgramInfo> programs, string windowsFolder, IEnumerable<string> sharedFolders)
    {
        var shared = sharedFolders.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _windowsFolder = Normalize(windowsFolder);

        // A program registered on a whole drive or on a folder shared by many apps would claim everything in it.
        _locations = programs
            .Where(program => !string.IsNullOrWhiteSpace(program.Location))
            .Select(program => (Program: program, Location: Normalize(program.Location!)))
            .Where(candidate => candidate.Location.Length > 3 && !shared.Contains(candidate.Location))
            .OrderByDescending(candidate => candidate.Location.Length)
            .ToList();

        _byName = programs
            .Where(program => Compact(program.Name).Length >= 4)
            .GroupBy(program => Compact(program.Name))
            .ToDictionary(group => group.Key, group => group.First());
    }

    public IReadOnlyList<RunningAppUsage> Compute(
        IReadOnlyList<ProcessSample> previous,
        IReadOnlyList<ProcessSample> current,
        TimeSpan elapsed,
        int processorCount)
    {
        var before = previous
            .Where(sample => sample.ProcessorTime is not null)
            .GroupBy(Identity)
            .ToDictionary(group => group.Key, group => group.First().ProcessorTime!.Value);
        var capacity = elapsed.TotalMilliseconds * Math.Max(1, processorCount);

        return current
            .Select(sample => (Sample: sample, Group: GroupOf(sample)))
            .GroupBy(entry => entry.Group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First().Group;
                var cpuMilliseconds = group.Sum(entry => CpuMilliseconds(entry.Sample, before));
                return new RunningAppUsage(
                    group.Key,
                    first.DisplayName,
                    first.Program,
                    group.Select(entry => entry.Sample.ExecutablePath).FirstOrDefault(path => path is not null),
                    capacity > 0 ? Math.Clamp(cpuMilliseconds / capacity * 100, 0, 100) : 0,
                    group.Sum(entry => entry.Sample.MemoryBytes),
                    group.Count());
            })
            .ToList();
    }

    public ProgramInfo? FindProgram(string executablePath)
    {
        if (_programByPath.TryGetValue(executablePath, out var cached))
        {
            return cached;
        }

        var path = Normalize(executablePath);
        var match = _locations.FirstOrDefault(candidate => path.StartsWith(candidate.Location + "\\", StringComparison.OrdinalIgnoreCase));
        return _programByPath[executablePath] = match.Program;
    }

    private (string Key, string DisplayName, ProgramInfo? Program) GroupOf(ProcessSample sample)
    {
        if (sample.ExecutablePath is not { } path)
        {
            return (ProtectedKey, "Processus protégés de Windows", null);
        }

        if (FindProgram(path) is { } program)
        {
            return ($"app:{program.Name}", program.Name, program);
        }

        if (Normalize(path).StartsWith(_windowsFolder + "\\", StringComparison.OrdinalIgnoreCase))
        {
            return (WindowsKey, "Windows", null);
        }

        // Some installers (Chrome among them) register no folder at all; the executable's own
        // description or file name then usually carries the program's name.
        if (_byName.TryGetValue(Compact(sample.Description), out var described) || _byName.TryGetValue(Compact(Path.GetFileNameWithoutExtension(path)), out described))
        {
            return ($"app:{described.Name}", described.Name, described);
        }

        var name = string.IsNullOrWhiteSpace(sample.Description) ? sample.Name : sample.Description.Trim();
        return ($"exe:{Path.GetFileName(path)}", name, null);
    }

    private static double CpuMilliseconds(ProcessSample sample, Dictionary<(int, DateTime?), TimeSpan> before)
    {
        // Without an earlier measure there is no interval: counting the whole lifetime would show absurd peaks.
        if (sample.ProcessorTime is not { } now || !before.TryGetValue(Identity(sample), out var then))
        {
            return 0;
        }

        return Math.Max(0, (now - then).TotalMilliseconds);
    }

    // Windows reuses process ids, so the start time tells a new process apart from an old one with the same id.
    private static (int, DateTime?) Identity(ProcessSample sample) => (sample.Id, sample.StartTime);

    private static string Compact(string? text) =>
        text is null ? string.Empty : new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Normalize(string path) => path.Trim().Trim('"').TrimEnd('\\', '/').Replace('/', '\\');
}
