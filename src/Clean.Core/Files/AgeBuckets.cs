namespace Clean.Core.Files;

public static class AgeBuckets
{
    public static IReadOnlyList<string> Labels { get; } = ["Moins d'1 mois", "1 à 6 mois", "6 à 12 mois", "1 à 2 ans", "Plus de 2 ans"];

    public static int IndexOf(DateTime lastWriteUtc, DateTime nowUtc)
    {
        var days = (nowUtc - lastWriteUtc).TotalDays;
        return days switch
        {
            < 30 => 0,
            < 182 => 1,
            < 365 => 2,
            < 730 => 3,
            _ => 4,
        };
    }
}
