using Clean.Core.Models;

namespace Clean.Tests.Models;

public class PathValidationResultTests
{
    [Fact]
    public void Valid_KeepsCanonicalPath()
    {
        var result = PathValidationResult.Valid(@"C:\Temp");

        Assert.True(result.IsValid);
        Assert.Equal(@"C:\Temp", result.CanonicalPath);
    }

    [Fact]
    public void Rejected_HasNoCanonicalPath()
    {
        var result = PathValidationResult.Rejected("Volume root");

        Assert.False(result.IsValid);
        Assert.Null(result.CanonicalPath);
        Assert.Equal("Volume root", result.Reason);
    }
}
