using Clean.Core.Models;

namespace Clean.Core.Developer;

// LastWriteUtc: the most recent change among the files inside, which tells whether the project is in use.
public sealed record DevFolder(
    string Path,
    DevFolderKind Kind,
    long SizeBytes,
    DateTime LastWriteUtc,
    IReadOnlyList<FoundFile> Files)
{
    public string Name => System.IO.Path.GetFileName(Path);

    public string ProjectFolder => System.IO.Path.GetDirectoryName(Path) ?? Path;

    public string ProjectName => System.IO.Path.GetFileName(ProjectFolder);
}

public sealed record DeveloperScanResult(IReadOnlyList<DevFolder> Folders, long FilesExamined, TimeSpan Duration);
