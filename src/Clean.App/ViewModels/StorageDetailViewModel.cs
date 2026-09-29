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
    private const int CompositionCount = 4;
    private const int ListedProgramCount = 3;
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly StorageUsage _usage;
    private readonly DetailNavigation _navigation;
    private AppProfile? _app;
    private string _summary;
    private string _advice;
    private bool _isLoading;
    private string? _loadingMessage;

    public StorageDetailViewModel(
        StorageUsage usage,
        long analyzedBytes,
        IReadOnlyList<StorageUsage> groupedItems,
        IFileExplorer fileExplorer,
        DetailNavigation navigation)
    {
        _usage = usage;
        _navigation = navigation;
        var description = LocationGuide.Describe(usage);

        Name = usage.Label;
        PathText = usage.Path ?? string.Empty;
        Kind = description.Kind;
        Verdict = description.Verdict.ToUpper(French);
        Risk = description.Risk;
        _summary = description.Summary;
        _advice = description.Advice;
        _app = AppGuide.Find(usage.IsDirectory ? usage.Label : Path.GetFileNameWithoutExtension(usage.Label));
        SizeText = ByteSize.Format(usage.SizeBytes);
        ShareText = analyzedBytes > 0
            ? $"{(usage.SizeBytes * 100.0 / analyzedBytes).ToString("0.#", French)} % de l'espace analysé"
            : string.Empty;

        IsGroup = usage.Path is null;
        CanHaveChildren = IsGroup || usage.IsDirectory;
        ChildrenTitle = IsGroup ? "ÉLÉMENTS REGROUPÉS" : "CE QU'IL CONTIENT";
        BackText = $"‹ {navigation.BackLabel}";

        CloseCommand = new RelayCommand(navigation.Close);
        BackCommand = new RelayCommand(() => navigation.GoBack?.Invoke(), () => navigation.GoBack is not null);
        RevealCommand = new RelayCommand(() => fileExplorer.Reveal(usage.Path!), () => usage.Path is not null);

        if (IsGroup)
        {
            ShowChildren(groupedItems);
        }
    }

    public StorageUsage Usage => _usage;

    public string Name { get; }

    public string PathText { get; }

    public bool HasPath => !IsGroup;

    public LocationKind Kind { get; }

    public string Verdict { get; }

    public RiskLevel Risk { get; }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string Advice
    {
        get => _advice;
        private set => SetProperty(ref _advice, value);
    }

    public string SizeText { get; }

    public string ShareText { get; }

    public bool IsGroup { get; }

    public bool CanReveal => !IsGroup;

    public bool CanGoBack => _navigation.GoBack is not null;

    public string BackText { get; }

    public bool CanHaveChildren { get; }

    public string ChildrenTitle { get; }

    public ObservableCollection<DetailFact> Facts { get; } = [];

    public ObservableCollection<StorageChildRow> Children { get; } = [];

    public RelayCommand CloseCommand { get; }

    public RelayCommand BackCommand { get; }

    public RelayCommand RevealCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? LoadingMessage
    {
        get => _loadingMessage;
        private set
        {
            if (SetProperty(ref _loadingMessage, value))
            {
                OnPropertyChanged(nameof(HasLoadingMessage));
            }
        }
    }

    public bool HasLoadingMessage => LoadingMessage is not null;

    public void OpenChild(StorageUsage child) => _navigation.OpenChild(child);

    public async Task LoadDetailsAsync(
        Func<StorageUsage, CancellationToken, Task<EntryDetails>> inspect,
        CancellationToken cancellationToken)
    {
        if (IsGroup)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var details = await inspect(_usage, cancellationToken);
            ShowFacts(details);
            ShowChildren(details.Children);

            if (_usage.IsDirectory && Children.Count == 0)
            {
                LoadingMessage = "Ce dossier est vide, ou son contenu n'est pas lisible sans droits administrateur.";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LoadingMessage = "Clean n'a pas l'autorisation de lire cet emplacement.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowFacts(EntryDetails details)
    {
        var now = DateTimeOffset.Now;
        ApplyProgramKnowledge(details.Programs);

        Facts.Add(new DetailFact("Type", DescribeType(details)));
        if (_app is not null)
        {
            Facts.Add(new DetailFact("Catégorie", _app.Category));
        }

        foreach (var fact in DescribePrograms(details.Programs))
        {
            Facts.Add(fact);
        }

        if (details.IsDirectory)
        {
            Facts.Add(new DetailFact(
                "Contenu",
                $"{details.FileCount.ToString("N0", French)} fichiers dans {details.FolderCount.ToString("N0", French)} dossiers"));
        }

        var composition = FileCategories.Summarize(details.Composition, CompositionCount);
        if (details.IsDirectory && composition.Count > 0)
        {
            Facts.Add(new DetailFact(
                "Composition",
                string.Join("  ·  ", composition.Select(share => $"{share.Category} {share.Percent.ToString("0", French)} %"))));
        }

        if (details.LastModified is { } lastModified)
        {
            Facts.Add(new DetailFact(details.IsDirectory ? "Dernière activité" : "Modifié le", RelativeDate.Format(lastModified, now)));
        }

        if (details.Created is { } created)
        {
            Facts.Add(new DetailFact("Créé le", RelativeDate.Format(created, now)));
        }
    }

    // The folder name alone is often not enough ("fiveml", "valo"): the application found inside it
    // (installed program or main executable) gives a far better explanation than "unknown folder".
    private void ApplyProgramKnowledge(IReadOnlyList<ProgramInfo> programs)
    {
        if (_app is not null || programs.Count != 1)
        {
            return;
        }

        var program = programs[0];
        _app = AppGuide.Find(program.Name);
        if (_app is null && Kind is not LocationKind.Folder)
        {
            return;
        }

        var description = LocationGuide.DescribeProgram(program);
        Summary = description.Summary;
        Advice = description.Advice;
    }

    private string DescribeType(EntryDetails details)
    {
        if (details.IsDirectory)
        {
            return "Dossier";
        }

        var extension = Path.GetExtension(_usage.Label);
        return string.IsNullOrEmpty(extension)
            ? "Fichier sans extension"
            : $"Fichier {extension.ToLower(French)} ({FileCategories.CategoryOf(extension).ToLower(French)})";
    }

    private static IEnumerable<DetailFact> DescribePrograms(IReadOnlyList<ProgramInfo> programs)
    {
        if (programs.Count == 1)
        {
            var program = programs[0];
            yield return new DetailFact("Application", DescribeProgram(program));
            if (program.InstalledOn is { } installedOn)
            {
                yield return new DetailFact("Installée le", installedOn.ToString("d MMMM yyyy", French));
            }
        }
        else if (programs.Count > 1)
        {
            var names = string.Join(", ", programs.Take(ListedProgramCount).Select(program => program.Name));
            var more = programs.Count > ListedProgramCount ? $" et {programs.Count - ListedProgramCount} autres" : string.Empty;
            yield return new DetailFact("Applications", $"{programs.Count} installées ici : {names}{more}");
        }
    }

    private static string DescribeProgram(ProgramInfo program)
    {
        var text = program.Name;
        if (!string.IsNullOrWhiteSpace(program.Publisher))
        {
            text += $" — {program.Publisher}";
        }

        if (!string.IsNullOrWhiteSpace(program.Version))
        {
            text += $", version {program.Version}";
        }

        return text;
    }

    private void ShowChildren(IEnumerable<StorageUsage> children)
    {
        var shown = StorageBuckets.TopWithRemainder(children, ChildCount);
        var largest = shown.Count > 0 ? shown.Max(child => child.SizeBytes) : 0;

        foreach (var child in shown)
        {
            var description = LocationGuide.Describe(child);
            Children.Add(new StorageChildRow(
                Usage: child,
                Label: child.Label,
                SizeText: ByteSize.Format(child.SizeBytes),
                Ratio: largest > 0 ? (double)child.SizeBytes / largest : 0,
                Summary: description.Summary,
                Risk: description.Risk,
                CanOpen: child.Path is not null));
        }
    }
}
