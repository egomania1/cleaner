using Clean.Core.Interfaces;

namespace Clean.Tests.Safety;

internal sealed class NoLinks : IReparsePointDetector
{
    public string? FindLinkOnPath(string path) => null;
}

internal sealed class LinkAt(string link) : IReparsePointDetector
{
    public string? FindLinkOnPath(string path) =>
        path.StartsWith(link, StringComparison.OrdinalIgnoreCase) ? link : null;
}
