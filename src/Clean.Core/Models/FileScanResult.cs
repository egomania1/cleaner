namespace Clean.Core.Models;

// CloudOnlyFileCount: OneDrive-style placeholders, skipped because reading them would download them.
public sealed record FileScanResult(IReadOnlyList<FoundFile> Files, long FilesExamined, long CloudOnlyFileCount, TimeSpan Duration);
