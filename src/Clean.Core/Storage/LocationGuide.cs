using Clean.Core.Models;

namespace Clean.Core.Storage;

public static class LocationGuide
{
    private const string NamePlaceholder = "{name}";

    private static readonly LocationDescription GroupedItems = new(
        LocationKind.Group,
        "Petits éléments",
        RiskLevel.Caution,
        "Plusieurs petits éléments regroupés pour que l'affichage reste lisible.",
        "Ils prennent peu de place ensemble : inutile de s'y attarder.");

    private static readonly LocationDescription UnknownFolder = new(
        LocationKind.Folder,
        "À examiner",
        RiskLevel.Caution,
        "Un dossier créé par une application, un jeu ou par toi. Ce n'est pas un emplacement standard de Windows.",
        "Clean ne sait pas à quoi il sert : ouvre-le pour voir son contenu. S'il appartient à une application, désinstalle-la plutôt que de supprimer le dossier.");

    private static readonly LocationDescription UnknownFile = new(
        LocationKind.File,
        "À examiner",
        RiskLevel.Caution,
        "Un fichier dont Clean ne connaît pas le type.",
        "Vérifie ce que c'est avant de le supprimer.");

    public static LocationDescription Describe(StorageUsage usage)
    {
        if (usage.Path is null)
        {
            return GroupedItems;
        }

        var segments = SegmentsFromDriveRoot(usage.Path);
        var rule = LocationCatalog.Rules.FirstOrDefault(candidate => candidate.Matches(segments));
        if (rule is not null)
        {
            return WithName(rule.Description, usage.Label);
        }

        if (usage.IsDirectory && LocationCatalog.AnywhereByName.TryGetValue(usage.Label, out var byName))
        {
            return byName;
        }

        if (!usage.IsDirectory)
        {
            return FileTypeGuide.Describe(usage.Label) ?? UnknownFile;
        }

        return UnknownFolder;
    }

    private static IReadOnlyList<string> SegmentsFromDriveRoot(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path[root.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static LocationDescription WithName(LocationDescription description, string name) =>
        description with
        {
            Summary = description.Summary.Replace(NamePlaceholder, name),
            Advice = description.Advice.Replace(NamePlaceholder, name),
        };
}
