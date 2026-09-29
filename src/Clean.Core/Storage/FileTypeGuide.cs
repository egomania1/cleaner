using Clean.Core.Models;

namespace Clean.Core.Storage;

internal static class FileTypeGuide
{
    private const string Check = "À examiner";

    private static readonly Dictionary<string, LocationDescription> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".iso"] = File("Une image de disque (copie complète d'un CD, DVD ou d'un installateur).", "Souvent inutile une fois le logiciel installé. Supprime-la si tu n'en as plus besoin."),
        [".img"] = File("Une image de disque.", "Souvent inutile une fois utilisée. Supprime-la si tu n'en as plus besoin."),
        [".zip"] = File("Une archive compressée.", "Si tu l'as déjà extraite, l'archive est souvent en double : vérifie avant de la supprimer."),
        [".rar"] = File("Une archive compressée.", "Si tu l'as déjà extraite, l'archive est souvent en double : vérifie avant de la supprimer."),
        [".7z"] = File("Une archive compressée.", "Si tu l'as déjà extraite, l'archive est souvent en double : vérifie avant de la supprimer."),
        [".exe"] = File("Un programme ou un installateur.", "Un installateur déjà utilisé peut être supprimé ; un programme en cours d'utilisation, non."),
        [".msi"] = File("Un installateur Windows.", "Peut être supprimé si le logiciel est déjà installé et que tu peux le retélécharger."),
        [".mp4"] = File("Une vidéo.", "Un fichier personnel : garde-le ou supprime-le selon ton choix."),
        [".mkv"] = File("Une vidéo.", "Un fichier personnel : garde-le ou supprime-le selon ton choix."),
        [".avi"] = File("Une vidéo.", "Un fichier personnel : garde-le ou supprime-le selon ton choix."),
        [".mov"] = File("Une vidéo.", "Un fichier personnel : garde-le ou supprime-le selon ton choix."),
        [".jpg"] = File("Une image.", "Un fichier personnel : garde-le ou supprime-le selon ton choix."),
        [".png"] = File("Une image.", "Un fichier personnel : garde-le ou supprime-le selon ton choix."),
        [".log"] = File("Un journal écrit par une application.", "Généralement inutile, sauf pour diagnostiquer un problème."),
        [".dmp"] = File("Un rapport de plantage (copie de la mémoire au moment d'une erreur).", "Utile seulement pour diagnostiquer un plantage. Peut être supprimé."),
        [".tmp"] = File("Un fichier temporaire.", "Peut généralement être supprimé s'il n'est plus utilisé par une application."),
        [".bak"] = File("Une copie de sauvegarde.", "Vérifie que l'original existe toujours avant de la supprimer."),
        [".dll"] = new(LocationKind.SystemFile, "Ne pas toucher", RiskLevel.Blocked, "Une bibliothèque utilisée par un programme.", "Ne la supprime pas : le programme qui l'utilise ne marcherait plus."),
        [".sys"] = new(LocationKind.SystemFile, "Ne pas toucher", RiskLevel.Blocked, "Un fichier système ou un pilote.", "Ne le supprime pas : Windows pourrait ne plus fonctionner correctement."),
    };

    public static LocationDescription? Describe(string fileName) =>
        ByExtension.TryGetValue(Path.GetExtension(fileName), out var description) ? description : null;

    private static LocationDescription File(string summary, string advice) =>
        new(LocationKind.File, Check, RiskLevel.Caution, summary, advice);
}
