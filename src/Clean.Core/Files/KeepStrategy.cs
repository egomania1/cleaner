using Clean.Core.Models;

namespace Clean.Core.Files;

public enum KeepStrategy
{
    Oldest,
    Newest,
    ShortestPath,
}

public static class Keeper
{
    // The copy that stays when the others are selected. Ties fall back to the path, so the choice is stable.
    public static FoundFile Choose(DuplicateGroup group, KeepStrategy strategy) => strategy switch
    {
        KeepStrategy.Newest => group.Files.OrderByDescending(file => file.LastWriteUtc).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).First(),
        KeepStrategy.ShortestPath => group.Files.OrderBy(file => file.Path.Length).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).First(),
        _ => group.Files.OrderBy(file => file.LastWriteUtc).ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase).First(),
    };
}
