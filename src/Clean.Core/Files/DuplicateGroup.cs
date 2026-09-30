using Clean.Core.Models;

namespace Clean.Core.Files;

// Files with byte-for-byte the same content. Every copy but one is wasted space.
public sealed record DuplicateGroup(string Id, long SizeBytes, IReadOnlyList<FoundFile> Files)
{
    public long WastedBytes => SizeBytes * (Files.Count - 1);
}
