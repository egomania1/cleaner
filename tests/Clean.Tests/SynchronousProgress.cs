namespace Clean.Tests;

// Progress<T> posts to a synchronization context, which makes assertions racy in tests.
internal sealed class SynchronousProgress<T> : IProgress<T>
{
    public List<T> Reports { get; } = [];

    public void Report(T value) => Reports.Add(value);
}
