namespace Clean.Core.Licensing;

public enum AccessMode
{
    Licensed,
    Trial,
    TrialEnded,
}

// What the user may do right now. Analysis, history and restoring are always free: someone who cleaned
// during the trial must be able to get their files back, licence or not.
public sealed record AppAccess(AccessMode Mode, int DaysLeft, string Title, string Detail)
{
    public bool CanClean => Mode != AccessMode.TrialEnded;

    public bool IsLicensed => Mode == AccessMode.Licensed;
}
