namespace Clean.Core.Models;

public sealed record ScanProgress(
    string CurrentScanner,
    string CurrentPath,
    long FilesScanned,
    long BytesAnalyzed,
    long PotentialRecoveryBytes,
    TimeSpan Elapsed);
