using Clean.Core.Models;

namespace Clean.App.ViewModels;

public sealed record StorageChildRow(
    StorageUsage Usage,
    string Label,
    string SizeText,
    double Ratio,
    string Summary,
    RiskLevel Risk,
    bool CanOpen);
