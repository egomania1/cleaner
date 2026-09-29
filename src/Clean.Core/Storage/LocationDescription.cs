using Clean.Core.Models;

namespace Clean.Core.Storage;

public sealed record LocationDescription(
    LocationKind Kind,
    string Verdict,
    RiskLevel Risk,
    string Summary,
    string Advice);
