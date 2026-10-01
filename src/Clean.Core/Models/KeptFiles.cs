namespace Clean.Core.Models;

public sealed record KeptFiles(KeptFileReason Reason, long FileCount, long SizeBytes);
