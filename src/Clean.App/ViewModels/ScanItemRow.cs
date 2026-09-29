using System.Globalization;
using Clean.Core.Formatting;
using Clean.Core.Models;

namespace Clean.App.ViewModels;

public sealed class ScanItemRow(ScanItem item)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public string Name => item.Name;

    public string Reason => item.Reason;

    public string Path => item.Path;

    public RiskLevel Risk => item.Risk;

    public string RiskText => item.Risk switch
    {
        RiskLevel.Safe => "SÛR",
        RiskLevel.Caution => "PRUDENCE",
        RiskLevel.Expert => "EXPERT",
        _ => "BLOQUÉ",
    };

    public string SizeText => ByteSize.Format(item.SizeBytes);

    public string ActionText => item.CanClean
        ? $"Supprimerait {ByteSize.Format(item.SizeBytes)} ({item.FileCount.ToString("N0", French)} fichiers)"
        : "Rien à supprimer pour l'instant";

    public string RuleText => $"Règle {item.RuleId}";

    public bool HasSkippedFiles => item.SkippedFileCount > 0;

    public string SkippedText => $"{item.SkippedFileCount.ToString("N0", French)} fichiers récents gardés, ils peuvent encore servir";
}
