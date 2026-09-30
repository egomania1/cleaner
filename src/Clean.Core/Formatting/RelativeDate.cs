using System.Globalization;

namespace Clean.Core.Formatting;

public static class RelativeDate
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Format(DateTimeOffset date, DateTimeOffset now) =>
        $"{date.ToString("d MMMM yyyy", French)} ({Ago(date, now)})";

    public static string Ago(DateTimeOffset date, DateTimeOffset now)
    {
        var days = (int)(now.Date - date.Date).TotalDays;
        return days switch
        {
            <= 0 => "aujourd'hui",
            1 => "hier",
            < 30 => $"il y a {days} jours",
            < 365 => $"il y a {days / 30} mois",
            < 730 => "il y a 1 an",
            _ => $"il y a {days / 365} ans",
        };
    }
}
