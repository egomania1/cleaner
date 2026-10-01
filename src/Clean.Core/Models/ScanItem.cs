namespace Clean.Core.Models;

public sealed record ScanItem(
    string Path,
    string Name,
    long SizeBytes,
    CleaningCategory Category,
    RiskLevel Risk,
    string Reason,
    string RuleId,
    DateTimeOffset? LastModified,
    bool CanClean,
    bool RequiresConfirmation,
    long FileCount,
    long SkippedFileCount,
    IReadOnlyList<KeptFiles>? KeptFiles = null);
