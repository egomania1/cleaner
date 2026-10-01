using System.Security.Cryptography;
using System.Text.Json;

namespace Clean.Core.Licensing;

// Token format: base64url(JSON payload) + "." + base64url(signature). The signature is ECDSA P-256 over the
// payload bytes with SHA-256, in the fixed-size (r || s) form that Node, Go and .NET all produce.
// Only the public key lives in the app; the private key never leaves the licence server.
public sealed class LicenseVerifier(byte[] publicKey, TimeSpan? grace = null)
{
    public const int SupportedVersion = 1;

    public static readonly TimeSpan DefaultGrace = TimeSpan.FromDays(3);

    // A PC clock a little behind the server's must not reject a token issued a minute ago.
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly TimeSpan _grace = grace ?? DefaultGrace;

    public LicenseCheck Verify(string? token, string deviceId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Invalid("Aucune licence.");
        }

        var parts = token.Trim().Split('.');
        if (parts.Length != 2)
        {
            return Invalid("Licence illisible.");
        }

        byte[] payload, signature;
        try
        {
            payload = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return Invalid("Licence illisible.");
        }

        if (!HasValidSignature(payload, signature))
        {
            return Invalid("La signature de la licence est invalide.");
        }

        LicenseToken? content;
        try
        {
            content = JsonSerializer.Deserialize<LicenseToken>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return Invalid("Licence illisible.");
        }

        if (content is null || content.Version != SupportedVersion || string.IsNullOrWhiteSpace(content.LicenseId) || content.ExpiresAt <= content.IssuedAt)
        {
            return Invalid("Licence non reconnue : mets Clean à jour.");
        }

        if (!string.IsNullOrEmpty(content.DeviceId) && !string.Equals(content.DeviceId, deviceId, StringComparison.Ordinal))
        {
            return Invalid("Cette licence est liée à un autre PC.");
        }

        if (now < content.IssuedAt - ClockTolerance)
        {
            return Invalid("La date de ce PC est en retard sur celle de la licence.");
        }

        if (now <= content.ExpiresAt)
        {
            return new LicenseCheck(LicenseState.Valid, content, "Licence valide.");
        }

        return now <= content.ExpiresAt + _grace
            ? new LicenseCheck(LicenseState.Grace, content, "Licence expirée : renouvelle-la pour garder l'accès.")
            : new LicenseCheck(LicenseState.Expired, content, "Licence expirée.");
    }

    private bool HasValidSignature(byte[] payload, byte[] signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(publicKey, out _);
            return key.VerifyData(payload, signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static LicenseCheck Invalid(string reason) => new(LicenseState.Invalid, null, reason);

    private static byte[] FromBase64Url(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
