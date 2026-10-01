namespace Clean.Core.Licensing;

// The plan names the website writes into a token.
public static class LicensePlans
{
    public const string Lifetime = "lifetime";

    // Full access for the person who owns the product: a normal signed token, with no end date and no device.
    public const string Owner = "owner";
}
