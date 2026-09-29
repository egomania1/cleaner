using Clean.Core.Formatting;

namespace Clean.Tests.Formatting;

public class RelativeDateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "29 septembre 2026 (aujourd'hui)")]
    [InlineData(1, "28 septembre 2026 (hier)")]
    [InlineData(12, "17 septembre 2026 (il y a 12 jours)")]
    [InlineData(95, "26 juin 2026 (il y a 3 mois)")]
    [InlineData(400, "25 août 2025 (il y a 1 an)")]
    [InlineData(1100, "25 septembre 2023 (il y a 3 ans)")]
    public void Format_WritesTheDateAndHowLongAgo(int daysAgo, string expected)
    {
        Assert.Equal(expected, RelativeDate.Format(Now.AddDays(-daysAgo), Now));
    }
}
