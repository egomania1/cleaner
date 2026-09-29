using Clean.Core.Models;

namespace Clean.Tests.Models;

public class ScanResultTests
{
    [Fact]
    public void Empty_HasNoItemsAndNoBytes()
    {
        Assert.Empty(ScanResult.Empty.Items);
        Assert.Equal(0, ScanResult.Empty.TotalBytes);
    }

    [Fact]
    public void CleanableBytes_OnlyCountsItemsThatCanBeCleaned()
    {
        var result = new ScanResult(
            [
                TestItems.Create(sizeBytes: 300, canClean: true),
                TestItems.Create(sizeBytes: 700, canClean: false),
            ],
            TimeSpan.FromSeconds(1));

        Assert.Equal(1000, result.TotalBytes);
        Assert.Equal(300, result.CleanableBytes);
    }
}
