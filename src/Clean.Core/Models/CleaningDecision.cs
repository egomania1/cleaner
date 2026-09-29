namespace Clean.Core.Models;

public sealed record CleaningDecision
{
    private CleaningDecision(ScanItem item, bool isAllowed, string reason)
    {
        Item = item;
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public ScanItem Item { get; }

    public bool IsAllowed { get; }

    public string Reason { get; }

    public static CleaningDecision Allow(ScanItem item, string reason)
    {
        if (item.Risk == RiskLevel.Blocked)
        {
            throw new InvalidOperationException($"'{item.Path}' is blocked and can never be allowed.");
        }

        return new CleaningDecision(item, true, reason);
    }

    public static CleaningDecision Deny(ScanItem item, string reason) => new(item, false, reason);
}
