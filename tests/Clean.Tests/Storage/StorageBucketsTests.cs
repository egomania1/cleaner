using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Storage;

public class StorageBucketsTests
{
    [Fact]
    public void TopWithRemainder_SortsBySizeAndGroupsTheRest()
    {
        StorageUsage[] usages =
        [
            new("Users", 300),
            new("Windows", 500),
            new("temp", 5),
            new("Program Files", 200),
            new("$SysReset", 1),
        ];

        var buckets = StorageBuckets.TopWithRemainder(usages, topCount: 3);

        Assert.Equal(["Windows", "Users", "Program Files", "Autres (2 éléments)"], buckets.Select(bucket => bucket.Label));
        Assert.Equal(6, buckets[^1].SizeBytes);
    }

    [Fact]
    public void TopWithRemainder_HasNoRemainder_WhenEverythingFits()
    {
        var buckets = StorageBuckets.TopWithRemainder([new("Windows", 500), new("Users", 300)], topCount: 8);

        Assert.Equal(2, buckets.Count);
    }

    [Fact]
    public void TopWithRemainder_IgnoresEmptyEntries()
    {
        var buckets = StorageBuckets.TopWithRemainder([new("Windows", 500), new("Empty", 0)], topCount: 8);

        Assert.Single(buckets);
    }
}
