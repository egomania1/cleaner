using System.Globalization;

namespace Clean.Core.Formatting;

public static class ByteSize
{
    private const double Unit = 1024;
    private static readonly string[] Suffixes = ["o", "Ko", "Mo", "Go", "To", "Po"];
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "A size cannot be negative.");
        }

        double size = bytes;
        var suffixIndex = 0;
        while (size >= Unit && suffixIndex < Suffixes.Length - 1)
        {
            size /= Unit;
            suffixIndex++;
        }

        return $"{size.ToString("0.#", French)} {Suffixes[suffixIndex]}";
    }
}
