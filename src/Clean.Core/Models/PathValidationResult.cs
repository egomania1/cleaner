namespace Clean.Core.Models;

public sealed record PathValidationResult
{
    private PathValidationResult(bool isValid, string? canonicalPath, string reason)
    {
        IsValid = isValid;
        CanonicalPath = canonicalPath;
        Reason = reason;
    }

    public bool IsValid { get; }

    public string? CanonicalPath { get; }

    public string Reason { get; }

    public static PathValidationResult Valid(string canonicalPath) =>
        new(true, canonicalPath, "Path is inside an allowed location.");

    public static PathValidationResult Rejected(string reason) => new(false, null, reason);
}
