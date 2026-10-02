namespace Clean.Core.Uninstall;

public sealed record LeftoverFolder(string Path, long SizeBytes, long FileCount, string Reason);

// StillInstalled: the program is still registered, so nothing it left can be called a leftover.
public sealed record LeftoverSearch(bool StillInstalled, IReadOnlyList<LeftoverFolder> Folders);
