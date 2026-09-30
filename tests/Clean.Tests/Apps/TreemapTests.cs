using Clean.Core.Apps;

namespace Clean.Tests.Apps;

public class TreemapTests
{
    [Fact]
    public void Layout_GivesEveryTileAnAreaProportionalToItsValue()
    {
        double[] values = [60, 25, 10, 5];

        var rects = Treemap.Layout(values, 400, 300);

        for (var index = 0; index < values.Length; index++)
        {
            Assert.Equal(values[index] / 100 * 400 * 300, rects[index].Width * rects[index].Height, 3);
        }
    }

    [Fact]
    public void Layout_StaysInsideTheBoundsWithoutOverlaps()
    {
        double[] values = [40, 30, 12, 9, 5, 3, 1];

        var rects = Treemap.Layout(values, 500, 200);

        foreach (var rect in rects)
        {
            Assert.InRange(rect.X, -0.001, 500);
            Assert.InRange(rect.Y, -0.001, 200);
            Assert.True(rect.X + rect.Width <= 500.001);
            Assert.True(rect.Y + rect.Height <= 200.001);
        }

        for (var first = 0; first < rects.Count; first++)
        {
            for (var second = first + 1; second < rects.Count; second++)
            {
                Assert.False(Overlap(rects[first], rects[second]), $"tiles {first} and {second} overlap");
            }
        }
    }

    [Fact]
    public void Layout_KeepsTheInputOrderForTheResults()
    {
        var rects = Treemap.Layout([1, 100], 100, 100);

        Assert.True(rects[1].Width * rects[1].Height > rects[0].Width * rects[0].Height);
    }

    [Fact]
    public void Layout_LeavesZeroValuesEmpty()
    {
        var rects = Treemap.Layout([0, 10], 100, 100);

        Assert.Equal(0, rects[0].Width * rects[0].Height);
        Assert.Equal(10_000, rects[1].Width * rects[1].Height, 3);
    }

    private static bool Overlap(TreemapRect a, TreemapRect b) =>
        a.X + 0.001 < b.X + b.Width && b.X + 0.001 < a.X + a.Width
        && a.Y + 0.001 < b.Y + b.Height && b.Y + 0.001 < a.Y + a.Height;
}
