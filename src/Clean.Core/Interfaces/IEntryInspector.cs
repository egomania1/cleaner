using Clean.Core.Models;

namespace Clean.Core.Interfaces;

public interface IEntryInspector
{
    Task<EntryDetails> InspectAsync(string path, bool isDirectory, CancellationToken cancellationToken);
}
