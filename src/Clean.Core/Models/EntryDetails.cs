namespace Clean.Core.Models;

public sealed record EntryDetails(
    bool IsDirectory,
    DateTimeOffset? Created,
    DateTimeOffset? LastModified,
    long FileCount,
    long FolderCount,
    IReadOnlyList<ExtensionUsage> Composition,
    IReadOnlyList<ProgramInfo> Programs,
    IReadOnlyList<StorageUsage> Children);
