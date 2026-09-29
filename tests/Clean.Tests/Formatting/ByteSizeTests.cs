using Clean.Core.Formatting;

namespace Clean.Tests.Formatting;

public class ByteSizeTests
{
    [Theory]
    [InlineData(0, "0 o")]
    [InlineData(512, "512 o")]
    [InlineData(1024, "1 Ko")]
    [InlineData(1536, "1,5 Ko")]
    [InlineData(5L * 1024 * 1024, "5 Mo")]
    [InlineData(812L * 1024 * 1024 * 1024, "812 Go")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1 To")]
    public void Format_UsesFrenchUnitsAndDecimalComma(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSize.Format(bytes));
    }

    [Fact]
    public void Format_RejectsNegativeSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSize.Format(-1));
    }
}
