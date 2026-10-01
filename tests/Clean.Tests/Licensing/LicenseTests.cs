using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clean.Core.Licensing;
using Clean.Infrastructure.Licensing;

namespace Clean.Tests.Licensing;

public sealed class LicenseTests : IDisposable
{
    private const string Device = "device-a";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ECDsa _serverKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly TestDirectory _folder = new();

    public void Dispose()
    {
        _serverKey.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public void Verify_AcceptsAValidTokenForThisDevice()
    {
        var check = Verifier().Verify(Sign(Token()), Device, Now);

        Assert.Equal(LicenseState.Valid, check.State);
        Assert.True(check.AllowsPaidFeatures);
        Assert.Equal("LIC-1", check.Token!.LicenseId);
    }

    [Fact]
    public void Verify_AcceptsAnyDeviceWhenTheTokenIsNotBound()
    {
        var check = Verifier().Verify(Sign(Token() with { DeviceId = null }), "another-pc", Now);

        Assert.Equal(LicenseState.Valid, check.State);
    }

    [Fact]
    public void Verify_RejectsATokenBoundToAnotherDevice()
    {
        var check = Verifier().Verify(Sign(Token()), "another-pc", Now);

        Assert.Equal(LicenseState.Invalid, check.State);
        Assert.False(check.AllowsPaidFeatures);
    }

    [Fact]
    public void Verify_GivesAGracePeriodThenExpires()
    {
        var token = Sign(Token() with { ExpiresAt = Now.AddDays(-1) });

        Assert.Equal(LicenseState.Grace, Verifier().Verify(token, Device, Now).State);
        Assert.True(Verifier().Verify(token, Device, Now).AllowsPaidFeatures);
        Assert.Equal(LicenseState.Expired, Verifier().Verify(token, Device, Now.AddDays(5)).State);
        Assert.False(Verifier().Verify(token, Device, Now.AddDays(5)).AllowsPaidFeatures);
    }

    [Fact]
    public void Verify_RejectsAPayloadEditedAfterSigning()
    {
        var parts = Sign(Token() with { ExpiresAt = Now.AddDays(1) }).Split('.');
        var forged = Encode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Token() with { ExpiresAt = Now.AddYears(50) }, Json)));

        var check = Verifier().Verify($"{forged}.{parts[1]}", Device, Now);

        Assert.Equal(LicenseState.Invalid, check.State);
    }

    [Fact]
    public void Verify_RejectsATokenSignedWithAnotherKey()
    {
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var check = Verifier().Verify(Sign(Token(), attacker), Device, Now);

        Assert.Equal(LicenseState.Invalid, check.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-dot")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    [InlineData("e30.e30")]
    public void Verify_RejectsGarbageWithoutThrowing(string? token)
    {
        Assert.Equal(LicenseState.Invalid, Verifier().Verify(token, Device, Now).State);
    }

    [Fact]
    public void Verify_RejectsAnUnsupportedVersion()
    {
        Assert.Equal(LicenseState.Invalid, Verifier().Verify(Sign(Token() with { Version = 2 }), Device, Now).State);
    }

    [Fact]
    public void Verify_RejectsATokenIssuedFarInTheFuture()
    {
        var token = Sign(Token() with { IssuedAt = Now.AddDays(10), ExpiresAt = Now.AddDays(40) });

        Assert.Equal(LicenseState.Invalid, Verifier().Verify(token, Device, Now).State);
    }

    [Fact]
    public void Verify_ToleratesASmallClockDifference()
    {
        var token = Sign(Token() with { IssuedAt = Now.AddHours(2), ExpiresAt = Now.AddDays(30) });

        Assert.Equal(LicenseState.Valid, Verifier().Verify(token, Device, Now).State);
    }

    [Fact]
    public void Verify_RejectsAMalformedPublicKeyWithoutThrowing()
    {
        var check = new LicenseVerifier([1, 2, 3]).Verify(Sign(Token()), Device, Now);

        Assert.Equal(LicenseState.Invalid, check.State);
    }

    [Fact]
    public void DeviceIdentity_IsStableAndDoesNotExposeTheMachineId()
    {
        var id = DeviceIdentity.Current();

        Assert.Equal(id, DeviceIdentity.Current());
        Assert.Equal(32, id.Length);
        Assert.DoesNotContain(Environment.MachineName, id, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Store_SavesLoadsAndDeletesTheToken()
    {
        var store = new LicenseStore(Path.Combine(_folder.RootPath, "sub", "license.key"));
        Assert.Null(store.Load());

        store.Save("  token.value\n");
        Assert.Equal("token.value", store.Load());

        store.Delete();
        Assert.Null(store.Load());
    }

    private static LicenseToken Token() => new(1, "LIC-1", "annual", Now.AddDays(-10), Now.AddDays(355), Device);

    private LicenseVerifier Verifier() => new(_serverKey.ExportSubjectPublicKeyInfo());

    private string Sign(LicenseToken token, ECDsa? key = null)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(token, Json));
        var signature = (key ?? _serverKey).SignData(payload, HashAlgorithmName.SHA256);
        return $"{Encode(payload)}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
