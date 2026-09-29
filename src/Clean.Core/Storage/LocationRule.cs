namespace Clean.Core.Storage;

// A pattern is a path relative to the drive root, where "*" stands for any single folder name,
// e.g. @"Users\*\AppData\Local\Temp". "{name}" in the texts is replaced by the matched entry's name.
internal sealed record LocationRule(string Pattern, LocationDescription Description)
{
    private readonly string[] _segments = Pattern.Split('\\');

    public bool Matches(IReadOnlyList<string> pathSegments)
    {
        if (pathSegments.Count != _segments.Length)
        {
            return false;
        }

        for (var index = 0; index < _segments.Length; index++)
        {
            if (_segments[index] != "*" && !string.Equals(_segments[index], pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
