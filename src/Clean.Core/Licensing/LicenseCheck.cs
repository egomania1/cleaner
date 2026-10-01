namespace Clean.Core.Licensing;

public enum LicenseState
{
    Valid,

    // Past its end date but still working for a few days, so a late renewal payment does not lock anyone out.
    Grace,
    Expired,
    Invalid,
}

public sealed record LicenseCheck(LicenseState State, LicenseToken? Token, string Reason)
{
    public bool AllowsPaidFeatures => State is LicenseState.Valid or LicenseState.Grace;
}
