using System.Text.Json;
using Clean.Core.Licensing;

namespace Clean.Tests.Licensing;

// The tokens in interop.json were signed by the website's code (Node.js). The app must accept exactly those.
public sealed class NodeInteropTests
{
    private readonly JsonElement _fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "interop.json"))).RootElement;

    [Fact]
    public void AValidTokenFromTheServerIsAccepted()
    {
        var check = Check("lifetime");

        Assert.Equal(LicenseState.Valid, check.State);
        Assert.Equal("LIC-INTEROP-1", check.Token!.LicenseId);
        Assert.Equal("lifetime", check.Token.Plan);
        Assert.Null(check.Token.DeviceId);
    }

    [Fact]
    public void TheOwnerTokenFromTheServerGivesTheOwnerAccess()
    {
        var check = Check("owner");
        var access = LicenseEvaluator.Evaluate(check, new TrialRecord(Now().AddDays(-100), Now().AddDays(-100)), Now());

        Assert.Equal(LicenseState.Valid, check.State);
        Assert.Equal("Compte propriétaire", access.Title);
        Assert.True(access.CanClean);
    }

    [Fact]
    public void ADeviceBoundTokenWorksOnlyOnItsDevice()
    {
        Assert.Equal(LicenseState.Valid, Check("boundToDevice").State);
        Assert.Equal(LicenseState.Invalid, Check("boundToAnotherDevice").State);
    }

    [Fact]
    public void AnExpiredTokenIsExpired()
    {
        Assert.Equal(LicenseState.Expired, Check("expired").State);
    }

    [Fact]
    public void AnEditedTokenIsRejected()
    {
        var token = _fixture.GetProperty("tokens").GetProperty("lifetime").GetString()!;
        var parts = token.Split('.');
        var edited = parts[0][..^2] + (parts[0][^2] == 'A' ? "B" : "A") + parts[0][^1] + "." + parts[1];

        Assert.Equal(LicenseState.Invalid, Verifier().Verify(edited, Device(), Now()).State);
    }

    private LicenseCheck Check(string name) =>
        Verifier().Verify(_fixture.GetProperty("tokens").GetProperty(name).GetString(), Device(), Now());

    private LicenseVerifier Verifier() => new(Convert.FromBase64String(_fixture.GetProperty("publicKeyDerBase64").GetString()!));

    private string Device() => _fixture.GetProperty("device").GetString()!;

    private DateTimeOffset Now() => DateTimeOffset.Parse(_fixture.GetProperty("now").GetString()!);
}
