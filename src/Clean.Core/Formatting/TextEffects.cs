using System.Globalization;
using System.Text.RegularExpressions;

namespace Clean.Core.Formatting;

// Frames of the text animations. Progress goes from 0 to 1; at 1 the exact target text is returned.
public static partial class TextEffects
{
    private const string Symbols = "~#%&X*$@";
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static bool CanAnimate(string? text) => !string.IsNullOrEmpty(text) && text.Any(char.IsLetterOrDigit);

    // Characters are revealed from left to right. Spaces stay spaces so words keep their shape while scrambling.
    public static string Decrypt(string target, double progress, Random random)
    {
        if (progress >= 1)
        {
            return target;
        }

        var revealed = (int)(target.Length * Math.Max(progress, 0));
        var characters = target.ToCharArray();
        for (var index = revealed; index < characters.Length; index++)
        {
            if (!char.IsWhiteSpace(characters[index]))
            {
                characters[index] = Symbols[random.Next(Symbols.Length)];
            }
        }

        return new string(characters);
    }

    // Only the first number moves; the unit and the words around it are kept as they are.
    public static string CountUp(string target, double progress)
    {
        if (progress >= 1)
        {
            return target;
        }

        var match = NumberPattern().Match(target);
        if (!match.Success)
        {
            return target;
        }

        var number = match.Value;
        var digits = new string(number.Where(character => char.IsDigit(character) || character == ',').ToArray()).Replace(',', '.');
        var value = double.Parse(digits, CultureInfo.InvariantCulture);
        var commaIndex = number.IndexOf(',');
        var decimals = commaIndex < 0 ? 0 : number.Length - commaIndex - 1;
        var format = number.Any(IsGroupSeparator) ? $"N{decimals}" : $"F{decimals}";

        var current = (value * Math.Clamp(progress, 0, 1)).ToString(format, French);
        return string.Concat(target.AsSpan(0, match.Index), current, target.AsSpan(match.Index + match.Length));
    }

    private static bool IsGroupSeparator(char character) => character is ' ' or ' ' or ' ';

    [GeneratedRegex(@"\d(?:[\d   ]*\d)?(?:,\d+)?")]
    private static partial Regex NumberPattern();
}
