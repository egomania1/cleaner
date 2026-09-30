using Clean.Core.Files;
using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public sealed record DuplicateProgress(long BytesToCompare, long BytesCompared, string CurrentPath);

public interface IDuplicateFinder
{
    Task<IReadOnlyList<DuplicateGroup>> FindAsync(IReadOnlyList<FoundFile> files, IProgress<DuplicateProgress>? progress, CancellationToken cancellationToken);
}
