namespace Clean.Core.Models;

public sealed record ScanResult(IReadOnlyList<ScanItem> Items, IReadOnlyList<ScanError> Errors, TimeSpan Duration)
{
    public static ScanResult Empty { get; } = new([], [], TimeSpan.Zero);

    public long TotalBytes => Items.Sum(item => item.SizeBytes);

    public long CleanableBytes => Items.Where(item => item.CanClean).Sum(item => item.SizeBytes);
}
