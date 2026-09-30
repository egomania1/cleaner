namespace Clean.Core.Interfaces;

public interface IFolderSizer
{
    // Excluded folders are other apps nested inside this one (games inside a launcher folder), counted on their own.
    Task<long> MeasureAsync(string folder, IReadOnlyCollection<string> excludedFolders, CancellationToken cancellationToken);
}
