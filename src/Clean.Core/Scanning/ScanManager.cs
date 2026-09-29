using System.Diagnostics;
using Clean.Core.Interfaces;
using Clean.Core.Models;

namespace Clean.Core.Scanning;

public sealed class ScanManager(IEnumerable<IScanner> scanners) : IScanManager
{
    public IReadOnlyList<IScanner> Scanners { get; } = scanners.ToList();

    public async Task<ScanResult> RunAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var items = new List<ScanItem>();
        var errors = new List<ScanError>();

        foreach (var scanner in Scanners)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                items.AddRange(await scanner.ScanAsync(progress, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One broken scanner must not throw away what the others found; the failure is reported instead.
                errors.Add(new ScanError(scanner.Id, exception.Message));
            }
        }

        return new ScanResult(items, errors, stopwatch.Elapsed);
    }
}
