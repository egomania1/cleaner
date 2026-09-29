using Clean.Core.Models;

namespace Clean.Core.Storage;

public static class StorageBuckets
{
    public static IReadOnlyList<StorageUsage> TopWithRemainder(IEnumerable<StorageUsage> usages, int topCount)
    {
        var sorted = SortBySize(usages);
        var buckets = sorted.Take(topCount).ToList();
        var remainder = sorted.Skip(topCount).ToList();

        if (remainder.Count > 0)
        {
            buckets.Add(new StorageUsage($"Autres ({remainder.Count} éléments)", remainder.Sum(usage => usage.SizeBytes)));
        }

        return buckets;
    }

    public static IReadOnlyList<StorageUsage> Remainder(IEnumerable<StorageUsage> usages, int topCount) =>
        SortBySize(usages).Skip(topCount).ToList();

    private static List<StorageUsage> SortBySize(IEnumerable<StorageUsage> usages) =>
        usages
            .Where(usage => usage.SizeBytes > 0)
            .OrderByDescending(usage => usage.SizeBytes)
            .ToList();
}
