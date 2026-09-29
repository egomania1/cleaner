using Clean.Core.Models;

namespace Clean.Tests;

internal static class TestItems
{
    public static ScanItem Create(long sizeBytes = 1024, RiskLevel risk = RiskLevel.Safe, bool canClean = true) =>
        new(
            Path: @"C:\Users\Test\AppData\Local\Temp\file.tmp",
            Name: "file.tmp",
            SizeBytes: sizeBytes,
            Category: CleaningCategory.Temporary,
            Risk: risk,
            Reason: "Test item",
            RuleId: "TEST_RULE",
            LastModified: null,
            CanClean: canClean,
            RequiresConfirmation: false);
}
