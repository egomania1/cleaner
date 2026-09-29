using Clean.Core.Models;

namespace Clean.Core.Storage;

public static class LocationGuide
{
    private const string UninstallAdvice =
        "Pour libérer de la place, désinstalle les applications inutiles depuis Paramètres › Applications. " +
        "Ne supprime jamais un dossier ici à la main : l'application ne fonctionnerait plus et resterait à moitié installée.";

    private static readonly LocationDescription WindowsUpdateLeftovers = new(
        LocationKind.System,
        "Supprimable via Windows",
        RiskLevel.Caution,
        "Des fichiers temporaires laissés par une mise à jour ou une installation de Windows.",
        "Une fois la mise à jour terminée, supprime-les avec « Nettoyage de disque » › Nettoyer les fichiers système.");

    private static readonly Dictionary<string, LocationDescription> KnownLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Windows"] = new(
            LocationKind.System,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "Le système d'exploitation : Windows lui-même, ses pilotes, ses mises à jour et ses composants. Le sous-dossier WinSxS y est souvent le plus gros.",
            "Ne supprime rien ici à la main : le PC pourrait ne plus démarrer. Pour récupérer de la place, utilise « Nettoyage de disque » ou Paramètres › Système › Stockage."),
        ["Users"] = new(
            LocationKind.UserData,
            "À trier toi-même",
            RiskLevel.Caution,
            "Les profils des comptes du PC : Bureau, Documents, Téléchargements, Images, Vidéos, et le dossier caché AppData où les applications rangent leurs réglages et leurs caches.",
            "Ce sont surtout tes fichiers personnels : c'est à toi de trier. Les caches d'applications rangés dans AppData pourront être nettoyés par Clean dans une prochaine version."),
        ["Program Files"] = new(
            LocationKind.Applications,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "Les applications 64 bits installées sur le PC.",
            UninstallAdvice),
        ["Program Files (x86)"] = new(
            LocationKind.Applications,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "Les applications 32 bits installées sur le PC.",
            UninstallAdvice),
        ["ProgramData"] = new(
            LocationKind.Applications,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "Les données que les applications partagent entre tous les comptes : réglages, licences, bases de données, caches.",
            "Ne le vide pas à la main : certaines applications ne fonctionneraient plus. Désinstaller une application supprime en général aussi ses données ici."),
        ["$Recycle.Bin"] = new(
            LocationKind.RecycleBin,
            "Peut être vidé",
            RiskLevel.Safe,
            "La corbeille de ce disque : les fichiers que tu as supprimés et qui peuvent encore être restaurés.",
            "Tu peux la vider sans risque si tu n'as plus rien à y récupérer : clic droit sur la Corbeille › Vider la Corbeille."),
        ["pagefile.sys"] = new(
            LocationKind.SystemFile,
            "Réglage avancé",
            RiskLevel.Expert,
            "Le fichier d'échange : Windows s'en sert comme mémoire de secours quand la RAM est pleine.",
            "Géré automatiquement par Windows. Sa taille se règle dans Paramètres système avancés › Performances › Mémoire virtuelle. Ne le supprime pas."),
        ["swapfile.sys"] = new(
            LocationKind.SystemFile,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "Un petit fichier d'échange utilisé par les applications du Microsoft Store pour se mettre en pause et reprendre rapidement.",
            "Géré automatiquement par Windows, à laisser tel quel."),
        ["hiberfil.sys"] = new(
            LocationKind.SystemFile,
            "Réglage avancé",
            RiskLevel.Expert,
            "Le fichier de veille prolongée : il garde le contenu de la mémoire pour la veille prolongée et le démarrage rapide.",
            "Il disparaît si tu désactives la veille prolongée (commande « powercfg /h off » en administrateur), mais le démarrage rapide sera désactivé aussi."),
        ["System Volume Information"] = new(
            LocationKind.System,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "Les points de restauration du système et des données internes de Windows.",
            "Protégé par Windows. Pour réduire sa taille, règle l'espace réservé dans Protection du système (Propriétés système)."),
        ["Recovery"] = new(
            LocationKind.System,
            "Ne pas toucher",
            RiskLevel.Blocked,
            "L'environnement de récupération de Windows, utilisé pour réparer le PC s'il ne démarre plus.",
            "À laisser en place."),
        ["PerfLogs"] = new(
            LocationKind.System,
            "Sans importance",
            RiskLevel.Caution,
            "Les journaux de performance de Windows. Ce dossier est généralement vide ou presque.",
            "Il ne libérerait presque rien : inutile d'y toucher."),
        ["Windows.old"] = new(
            LocationKind.System,
            "Supprimable via Windows",
            RiskLevel.Caution,
            "L'ancienne installation de Windows, gardée après une mise à jour majeure pour pouvoir revenir en arrière.",
            "Supprime-la avec « Nettoyage de disque » › Nettoyer les fichiers système › Installation(s) précédente(s) de Windows. Tu ne pourras plus revenir à l'ancienne version."),
        ["$WINDOWS.~BT"] = WindowsUpdateLeftovers,
        ["$Windows.~WS"] = WindowsUpdateLeftovers,
        ["$WinREAgent"] = WindowsUpdateLeftovers,
        ["$SysReset"] = new(
            LocationKind.System,
            "Sans importance",
            RiskLevel.Caution,
            "Des journaux laissés par une réinitialisation ou une réparation de Windows.",
            "Ils ne prennent presque pas de place : inutile d'y toucher."),
    };

    private static readonly LocationDescription GroupedItems = new(
        LocationKind.Group,
        "Petits éléments",
        RiskLevel.Caution,
        "Plusieurs petits éléments regroupés pour que le graphique reste lisible.",
        "Regarde la liste ci-dessous pour savoir ce qu'ils contiennent.");

    private static readonly LocationDescription UnknownFolder = new(
        LocationKind.Folder,
        "À examiner",
        RiskLevel.Caution,
        "Un dossier créé par une application, un jeu ou par toi. Ce n'est pas un dossier standard de Windows.",
        "Clean ne sait pas à quoi il sert : regarde son contenu avant de décider. S'il appartient à une application, désinstalle-la plutôt que de supprimer le dossier.");

    private static readonly LocationDescription UnknownFile = new(
        LocationKind.File,
        "À examiner",
        RiskLevel.Caution,
        "Un fichier placé à la racine du disque.",
        "Clean ne sait pas à quoi il sert : vérifie ce que c'est avant de le supprimer.");

    public static LocationDescription Describe(StorageUsage usage)
    {
        if (usage.Path is null)
        {
            return GroupedItems;
        }

        if (KnownLocations.TryGetValue(usage.Label, out var known))
        {
            return known;
        }

        return usage.IsDirectory ? UnknownFolder : UnknownFile;
    }
}
