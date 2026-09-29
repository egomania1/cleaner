using System.Collections.ObjectModel;
using System.Globalization;
using Clean.Core.Formatting;
using Clean.Core.Interfaces;
using Clean.Core.Models;
using Clean.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clean.App.ViewModels;

public sealed class StorageDetailViewModel : ObservableObject
{
    private const int ChildCount = 6;
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly StorageUsage _usage;
    private bool _isLoadingChildren;
    private string? _childrenMessage;

    public StorageDetailViewModel(
        StorageUsage usage,
        long analyzedBytes,
        IReadOnlyList<StorageUsage> groupedItems,
        IFileExplorer fileExplorer,
        Action close)
    {
        _usage = usage;
        var description = LocationGuide.Describe(usage);

        Name = usage.Label;
        Verdict = description.Verdict.ToUpper(French);
        Risk = description.Risk;
        Summary = description.Summary;
        Advice = description.Advice;
        Kind = description.Kind;
        SizeText = ByteSize.Format(usage.SizeBytes);
        ShareText = analyzedBytes > 0
            ? $"{(usage.SizeBytes * 100.0 / analyzedBytes).ToString("0.#", French)} % de l'espace analysé"
            : string.Empty;

        IsGroup = usage.Path is null;
        CanHaveChildren = IsGroup || usage.IsDirectory;
        ChildrenTitle = IsGroup ? "ÉLÉMENTS REGROUPÉS" : "CE QU'IL CONTIENT";

        CloseCommand = new RelayCommand(close);
        RevealCommand = new RelayCommand(() => fileExplorer.Reveal(usage.Path!), () => usage.Path is not null);

        if (IsGroup)
        {
            ShowChildren(StorageBuckets.TopWithRemainder(groupedItems, ChildCount));
        }
    }

    public StorageUsage Usage => _usage;

    public string Name { get; }

    public string Verdict { get; }

    public RiskLevel Risk { get; }

    public string Summary { get; }

    public string Advice { get; }

    public LocationKind Kind { get; }

    public string SizeText { get; }

    public string ShareText { get; }

    public bool IsGroup { get; }

    public bool CanReveal => !IsGroup;

    public bool CanHaveChildren { get; }

    public string ChildrenTitle { get; }

    public ObservableCollection<StorageChildRow> Children { get; } = [];

    public RelayCommand CloseCommand { get; }

    public RelayCommand RevealCommand { get; }

    public bool IsLoadingChildren
    {
        get => _isLoadingChildren;
        private set => SetProperty(ref _isLoadingChildren, value);
    }

    public string? ChildrenMessage
    {
        get => _childrenMessage;
        private set
        {
            if (SetProperty(ref _childrenMessage, value))
            {
                OnPropertyChanged(nameof(HasChildrenMessage));
            }
        }
    }

    public bool HasChildrenMessage => ChildrenMessage is not null;

    public async Task LoadChildrenAsync(IStorageAnalyzer storageAnalyzer, CancellationToken cancellationToken)
    {
        if (!_usage.IsDirectory || _usage.Path is null)
        {
            return;
        }

        IsLoadingChildren = true;
        try
        {
            var children = await storageAnalyzer.AnalyzeAsync(_usage.Path, null, cancellationToken);
            ShowChildren(StorageBuckets.TopWithRemainder(children, ChildCount));

            if (Children.Count == 0)
            {
                ChildrenMessage = "Ce dossier est vide, ou son contenu n'est pas lisible sans droits administrateur.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ChildrenMessage = "Clean n'a pas l'autorisation de lire ce dossier.";
        }
        finally
        {
            IsLoadingChildren = false;
        }
    }

    private void ShowChildren(IReadOnlyList<StorageUsage> children)
    {
        var largest = children.Count > 0 ? children.Max(child => child.SizeBytes) : 0;
        foreach (var child in children)
        {
            var ratio = largest > 0 ? (double)child.SizeBytes / largest : 0;
            Children.Add(new StorageChildRow(child.Label, ByteSize.Format(child.SizeBytes), ratio));
        }
    }
}
