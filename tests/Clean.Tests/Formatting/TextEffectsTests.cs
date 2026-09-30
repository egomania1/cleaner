using Clean.Core.Formatting;

namespace Clean.Tests.Formatting;

public class TextEffectsTests
{
    [Theory]
    [InlineData("2,5 Go", true)]
    [InlineData("Stockage", true)]
    [InlineData("—", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void CanAnimate_NeedsALetterOrADigit(string? text, bool expected)
    {
        Assert.Equal(expected, TextEffects.CanAnimate(text));
    }

    [Fact]
    public void Decrypt_ReturnsTheTargetWhenFinished()
    {
        Assert.Equal("2,5 Go", TextEffects.Decrypt("2,5 Go", 1, new Random(1)));
    }

    [Fact]
    public void Decrypt_ScramblesOnlyTheUnrevealedPartAndKeepsSpaces()
    {
        var frame = TextEffects.Decrypt("812 Go libres", 0.5, new Random(1));

        Assert.Equal("812 Go libres".Length, frame.Length);
        Assert.StartsWith("812 Go", frame);
        Assert.Equal(' ', frame[6]);
        Assert.DoesNotContain("libres", frame);
    }

    [Fact]
    public void Decrypt_ScramblesEverythingAtTheStart()
    {
        var frame = TextEffects.Decrypt("512 Mo", 0, new Random(1));

        Assert.All(frame.Replace(" ", ""), character => Assert.Contains(character, "~#%&X*$@"));
    }

    [Theory]
    [InlineData("2,5 Go", 0, "0,0 Go")]
    [InlineData("2,5 Go", 0.6, "1,5 Go")]
    [InlineData("812 Go", 0.5, "406 Go")]
    [InlineData("120 Go / 476 Go", 0.5, "60 Go / 476 Go")]
    [InlineData("Aucun chiffre", 0.5, "Aucun chiffre")]
    public void CountUp_MovesTheFirstNumberAndKeepsItsFormat(string target, double progress, string expected)
    {
        Assert.Equal(expected, TextEffects.CountUp(target, progress));
    }

    [Fact]
    public void CountUp_KeepsThousandsSeparators()
    {
        Assert.Equal("617 000 fichiers", TextEffects.CountUp("1 234 000 fichiers", 0.5));
    }

    [Fact]
    public void CountUp_ReturnsTheExactTargetWhenFinished()
    {
        Assert.Equal("1 234 fichiers", TextEffects.CountUp("1 234 fichiers", 1));
    }
}
