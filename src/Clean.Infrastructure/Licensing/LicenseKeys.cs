namespace Clean.Infrastructure.Licensing;

public static class LicenseKeys
{
    // The licence server's public key (ECDSA P-256, SubjectPublicKeyInfo DER). Null until the website exists:
    // then no licence can be activated, and the trial is the only way in. See docs/LICENSING.md.
    public static readonly byte[]? PublicKey = null;
}
