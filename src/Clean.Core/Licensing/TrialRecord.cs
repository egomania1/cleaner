namespace Clean.Core.Licensing;

// StartedAt is the first launch. LastSeenAt only moves forward, so putting the PC clock back is noticed.
public sealed record TrialRecord(DateTimeOffset StartedAt, DateTimeOffset LastSeenAt);
