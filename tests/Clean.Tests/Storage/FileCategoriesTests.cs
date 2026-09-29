using Clean.Core.Models;
using Clean.Core.Storage;

namespace Clean.Tests.Storage;

public class FileCategoriesTests
{
    [Theory]
    [InlineData(".mp4", "Vidéos")]
    [InlineData(".MP4", "Vidéos")]
    [InlineData(".dll", "Bibliothèques")]
    [InlineData(".unknown", FileCategories.Other)]
    [InlineData("", FileCategories.Other)]
    public void CategoryOf_MapsExtensions(string extension, string expected)
    {
        Assert.Equal(expected, FileCategories.CategoryOf(extension));
    }

    [Fact]
    public void Summarize_GroupsByCategoryWithPercentages()
    {
        ExtensionUsage[] usages =
        [
            new(".mp4", 600, 2),
            new(".mkv", 150, 1),
            new(".zip", 200, 4),
            new(".txt", 50, 10),
        ];

        var shares = FileCategories.Summarize(usages, topCount: 2);

        Assert.Equal(["Vidéos", "Archives"], shares.Select(share => share.Category));
        Assert.Equal(75, shares[0].Percent);
        Assert.Equal(20, shares[1].Percent);
    }
}
