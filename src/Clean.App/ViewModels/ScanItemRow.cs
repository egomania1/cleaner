using System.Globalization;
using Clean.Core.Formatting;
using Clean.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clean.App.ViewModels;

public sealed class ScanItemRow : ObservableObject
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly Action _selectionChanged;
    private bool _isSelected;

    public ScanItemRow(ScanItem item, Action selectionChanged)
    {
        Item = item;
        _selectionChanged = selectionChanged;

        // Only what is proven harmless is ticked for the user; anything riskier has to be chosen on purpose.
        _isSelected = item.CanClean && item.Risk == RiskLevel.Safe;
    }

    public ScanItem Item { get; }

    public bool CanSelect => Item.CanClean;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value && CanSelect))
            {
                _selectionChanged();
            }
        }
    }

    public string Name => Item.Name;

    public string Reason => Item.Reason;

    public string Path => Item.Path;

    public RiskLevel Risk => Item.Risk;

    public string RiskText => Item.Risk switch
    {
        RiskLevel.Safe => "SÛR",
        RiskLevel.Caution => "PRUDENCE",
        RiskLevel.Expert => "EXPERT",
        _ => "BLOQUÉ",
    };

    public string SizeText => ByteSize.Format(Item.SizeBytes);

    public string ActionText => Item.CanClean
        ? $"Supprimera {ByteSize.Format(Item.SizeBytes)} ({Item.FileCount.ToString("N0", French)} fichiers)"
        : "Rien à supprimer pour l'instant";

    public string RuleText => $"Règle {Item.RuleId}";

    public bool HasSkippedFiles => Item.SkippedFileCount > 0;

    public string SkippedText => $"{Item.SkippedFileCount.ToString("N0", French)} fichiers récents gardés, ils peuvent encore servir";
}
