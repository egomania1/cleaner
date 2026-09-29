using Clean.Core.Models;

namespace Clean.Core.Storage;

public static class StorageBuckets
{
    public static IReadOnlyList<StorageUsage> TopWithRemainder(IEnumerable<StorageUsage> usages, int topCount)
    {
        var sorted = usages
            .Where(usage => usage.SizeBytes > 0)
            .OrderByDescending(usage => usage.SizeBytes)
            .ToList();

        var buckets = sorted.Take(topCount).ToList();
        var remainder = sorted.Skip(topCount).ToList();

        if (remainder.Count > 0)
        {
            buckets.Add(new StorageUsage($"Autres ({remainder.Count} éléments)", remainder.Sum(usage => usage.SizeBytes)));
        }

        return buckets;
    }
}
