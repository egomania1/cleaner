namespace Clean.Core.Licensing;

// What the licence server signs after a payment. It holds no name or e-mail: only an opaque licence id.
// DeviceId is empty for a licence that may be used on any PC.
public sealed record LicenseToken(
    int Version,
    string LicenseId,
    string Plan,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string? DeviceId);
