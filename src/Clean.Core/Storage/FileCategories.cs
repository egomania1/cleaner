using Clean.Core.Models;

namespace Clean.Core.Storage;

public static class FileCategories
{
    public const string Other = "Autres fichiers";

    private static readonly Dictionary<string, string> CategoryByExtension = BuildCategories(
        ("Vidéos", [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v"]),
        ("Images", [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff", ".psd", ".raw", ".ico", ".svg"]),
        ("Musique", [".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".wma"]),
        ("Documents", [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".odt", ".rtf", ".md", ".csv"]),
        ("Archives", [".zip", ".rar", ".7z", ".tar", ".gz", ".cab"]),
        ("Images disque", [".iso", ".img", ".vhd", ".vhdx", ".wim", ".esd"]),
        ("Programmes", [".exe", ".msi", ".msix", ".appx", ".bat", ".cmd", ".ps1"]),
        ("Bibliothèques", [".dll", ".sys", ".ocx", ".mui", ".node", ".so"]),
        ("Code source", [".js", ".ts", ".tsx", ".cs", ".py", ".java", ".cpp", ".c", ".h", ".json", ".xml", ".html", ".css", ".map", ".pdb"]),
        ("Journaux et rapports", [".log", ".etl", ".evtx", ".dmp"]),
        ("Fichiers temporaires", [".tmp", ".temp", ".cache"]),
        ("Données (jeux, bases…)", [".dat", ".db", ".sqlite", ".pak", ".bin", ".bundle", ".assets", ".vpk", ".arc", ".rpf", ".ldb"]),
        ("Polices", [".ttf", ".otf", ".fon", ".woff", ".woff2"]));

    public static string CategoryOf(string extension) =>
        CategoryByExtension.TryGetValue(extension, out var category) ? category : Other;

    public static IReadOnlyList<CategoryShare> Summarize(IEnumerable<ExtensionUsage> usages, int topCount)
    {
        var byCategory = usages
            .GroupBy(usage => CategoryOf(usage.Extension))
            .Select(group => (Category: group.Key, SizeBytes: group.Sum(usage => usage.SizeBytes)))
            .Where(entry => entry.SizeBytes > 0)
            .OrderByDescending(entry => entry.SizeBytes)
            .ToList();

        var total = byCategory.Sum(entry => entry.SizeBytes);
        return byCategory
            .Take(topCount)
            .Select(entry => new CategoryShare(entry.Category, entry.SizeBytes, total > 0 ? entry.SizeBytes * 100.0 / total : 0))
            .ToList();
    }

    private static Dictionary<string, string> BuildCategories(params (string Category, string[] Extensions)[] groups)
    {
        var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (category, extensions) in groups)
        {
            foreach (var extension in extensions)
            {
                categories[extension] = category;
            }
        }

        return categories;
    }
}

public sealed record CategoryShare(string Category, long SizeBytes, double Percent);
