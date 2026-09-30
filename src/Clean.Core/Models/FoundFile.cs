namespace Clean.Core.Models;

public sealed record FoundFile(string Path, long SizeBytes, DateTime LastWriteUtc)
{
    public string Name => System.IO.Path.GetFileName(Path);

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? Path;

    public string Extension => System.IO.Path.GetExtension(Path);
}
