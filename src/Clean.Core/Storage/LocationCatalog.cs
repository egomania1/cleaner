using Clean.Core.Models;

namespace Clean.Core.Storage;

// Explanations for well-known Windows locations. Rules are checked in order,
// so a specific pattern must come before a generic "*" one of the same depth.
internal static class LocationCatalog
{
    private const string DoNotTouch = "Ne pas toucher";
    private const string SortYourself = "À trier toi-même";
    private const string CanBeEmptied = "Peut être vidé";
    private const string ThroughWindows = "Supprimable via Windows";
    private const string Advanced = "Réglage avancé";
    private const string Negligible = "Sans importance";
    private const string Rebuilds = "Se recrée tout seul";

    private const string UninstallAdvice =
        "Pour libérer de la place, désinstalle l'application depuis Paramètres › Applications. " +
        "Ne supprime jamais ce dossier à la main : l'application resterait à moitié installée.";

    private const string SystemAdvice = "Ne supprime rien ici à la main : Windows ou tes applications pourraient ne plus fonctionner.";

    private const string PersonalAdvice = "Ce sont tes fichiers : Clean ne les supprimera jamais tout seul. Trie-les toi-même et garde ce qui compte.";

    public static IReadOnlyList<LocationRule> Rules { get; } =
    [
        // Drive root
        Rule("Windows", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Le système d'exploitation : Windows lui-même, ses pilotes, ses mises à jour et ses composants.",
            "Ne supprime rien ici à la main : le PC pourrait ne plus démarrer. Pour récupérer de la place, utilise « Nettoyage de disque » ou Paramètres › Système › Stockage."),
        Rule("Users", LocationKind.UserData, SortYourself, RiskLevel.Caution,
            "Les profils des comptes du PC : Bureau, Documents, Téléchargements, Images, Vidéos, et le dossier caché AppData où les applications rangent leurs réglages et leurs caches.",
            "Ouvre ton profil pour voir ce qui prend de la place. Tes fichiers personnels sont à trier toi-même."),
        Rule("Program Files", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les applications 64 bits installées sur le PC. Chaque sous-dossier correspond à une application ou à un éditeur.",
            UninstallAdvice),
        Rule("Program Files (x86)", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les applications 32 bits installées sur le PC. Chaque sous-dossier correspond à une application ou à un éditeur.",
            UninstallAdvice),
        Rule("ProgramData", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les données que les applications partagent entre tous les comptes : réglages, licences, bases de données, caches.",
            "Ne le vide pas à la main. Désinstaller une application supprime en général aussi ses données ici."),
        Rule("$Recycle.Bin", LocationKind.RecycleBin, CanBeEmptied, RiskLevel.Safe,
            "La corbeille de ce disque : les fichiers que tu as supprimés et qui peuvent encore être restaurés.",
            "Tu peux la vider sans risque si tu n'as plus rien à y récupérer : clic droit sur la Corbeille › Vider la Corbeille."),
        Rule("pagefile.sys", LocationKind.SystemFile, Advanced, RiskLevel.Expert,
            "Le fichier d'échange : Windows s'en sert comme mémoire de secours quand la RAM est pleine.",
            "Géré automatiquement par Windows. Sa taille se règle dans Paramètres système avancés › Performances › Mémoire virtuelle. Ne le supprime pas."),
        Rule("swapfile.sys", LocationKind.SystemFile, DoNotTouch, RiskLevel.Blocked,
            "Un petit fichier d'échange utilisé par les applications du Microsoft Store pour se mettre en pause et reprendre rapidement.",
            "Géré automatiquement par Windows, à laisser tel quel."),
        Rule("hiberfil.sys", LocationKind.SystemFile, Advanced, RiskLevel.Expert,
            "Le fichier de veille prolongée : il garde le contenu de la mémoire pour la veille prolongée et le démarrage rapide.",
            "Il disparaît si tu désactives la veille prolongée (commande « powercfg /h off » en administrateur), mais le démarrage rapide sera désactivé aussi."),
        Rule("System Volume Information", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les points de restauration du système et des données internes de Windows.",
            "Protégé par Windows. Pour réduire sa taille, règle l'espace réservé dans Protection du système (Propriétés système)."),
        Rule("Recovery", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "L'environnement de récupération de Windows, utilisé pour réparer le PC s'il ne démarre plus.",
            "À laisser en place."),
        Rule("PerfLogs", LocationKind.System, Negligible, RiskLevel.Caution,
            "Les journaux de performance de Windows. Ce dossier est généralement vide ou presque.",
            "Il ne libérerait presque rien : inutile d'y toucher."),
        Rule("Windows.old", LocationKind.System, ThroughWindows, RiskLevel.Caution,
            "L'ancienne installation de Windows, gardée après une mise à jour majeure pour pouvoir revenir en arrière.",
            "Supprime-la avec « Nettoyage de disque » › Nettoyer les fichiers système › Installation(s) précédente(s) de Windows. Tu ne pourras plus revenir à l'ancienne version."),
        UpdateLeftovers("$WINDOWS.~BT"),
        UpdateLeftovers("$Windows.~WS"),
        UpdateLeftovers("$WinREAgent"),
        Rule("$SysReset", LocationKind.System, Negligible, RiskLevel.Caution,
            "Des journaux laissés par une réinitialisation ou une réparation de Windows.",
            "Ils ne prennent presque pas de place : inutile d'y toucher."),

        // Inside Windows
        Rule(@"Windows\WinSxS", LocationKind.System, "Réductible via Windows", RiskLevel.Caution,
            "Le magasin de composants de Windows : toutes les versions des fichiers système, gardées pour installer les mises à jour et réparer Windows. Beaucoup de ses fichiers sont partagés avec System32, donc sa vraie taille est plus petite qu'affiché.",
            "Ne le supprime jamais à la main. Windows peut le réduire lui-même : « Nettoyage de disque » › Nettoyer les fichiers système › Nettoyage des mises à jour Windows."),
        Rule(@"Windows\System32", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Le cœur de Windows : programmes système, pilotes et bibliothèques 64 bits.",
            "Ne touche à rien ici : Windows ne démarrerait plus."),
        Rule(@"Windows\SysWOW64", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les fichiers système 32 bits, qui permettent de faire tourner les anciennes applications.",
            SystemAdvice),
        Rule(@"Windows\SystemApps", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les applications intégrées à Windows : menu Démarrer, recherche, Paramètres, Edge…",
            SystemAdvice),
        Rule(@"Windows\Microsoft.NET", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Le .NET Framework, un socle utilisé par beaucoup d'applications Windows.",
            SystemAdvice),
        Rule(@"Windows\assembly", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Le cache des bibliothèques .NET partagées entre les applications.",
            SystemAdvice),
        Rule(@"Windows\Installer", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les copies des programmes d'installation, dont Windows a besoin pour réparer, mettre à jour ou désinstaller tes applications.",
            "Ne le vide surtout pas à la main : certaines applications deviendraient impossibles à désinstaller."),
        Rule(@"Windows\SoftwareDistribution", LocationKind.Cache, ThroughWindows, RiskLevel.Caution,
            "Les fichiers téléchargés par Windows Update avant d'être installés.",
            "Supprimable avec « Nettoyage de disque » › Nettoyer les fichiers système › Nettoyage de Windows Update. Windows retéléchargera ce dont il a besoin."),
        Rule(@"Windows\Temp", LocationKind.Cache, CanBeEmptied, RiskLevel.Safe,
            "Les fichiers temporaires de Windows et des installations.",
            "Peut être vidé. Clean s'en chargera dans une prochaine version, en gardant les fichiers encore utilisés."),
        Rule(@"Windows\Logs", LocationKind.Cache, Negligible, RiskLevel.Caution,
            "Les journaux de Windows (installations, mises à jour, diagnostics).",
            "Ils prennent rarement beaucoup de place. Nettoyage de disque peut supprimer les plus anciens."),
        Rule(@"Windows\Prefetch", LocationKind.Cache, Rebuilds, RiskLevel.Caution,
            "Des données qui accélèrent le lancement des applications que tu utilises souvent.",
            "Inutile de le vider : il est petit et Windows le recrée aussitôt."),
        Rule(@"Windows\Fonts", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les polices de caractères installées.",
            "Pour retirer une police, passe par Paramètres › Personnalisation › Polices."),
        Rule(@"Windows\LiveKernelReports", LocationKind.Cache, CanBeEmptied, RiskLevel.Caution,
            "Des rapports de plantage du système (fichiers .dmp), parfois très gros.",
            "Utiles seulement pour diagnostiquer un problème. Nettoyage de disque peut les supprimer."),
        Rule(@"Windows\servicing", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les outils que Windows utilise pour installer ses mises à jour.",
            SystemAdvice),
        Rule(@"Windows\INF", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les fichiers d'installation des pilotes.",
            SystemAdvice),
        Rule(@"Windows\SystemResources", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les images, icônes et sons de l'interface de Windows.",
            SystemAdvice),

        // Inside Users
        Rule(@"Users\Public", LocationKind.UserData, SortYourself, RiskLevel.Caution,
            "Le dossier partagé entre tous les comptes du PC.",
            PersonalAdvice),
        Rule(@"Users\Default", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Le modèle utilisé par Windows pour créer les nouveaux comptes.",
            "À laisser tel quel."),
        Rule(@"Users\*", LocationKind.UserData, SortYourself, RiskLevel.Caution,
            "Le profil du compte « {name} » : son Bureau, ses Documents, ses Téléchargements et les données de ses applications.",
            "Ouvre-le pour voir ce qui prend de la place. Les fichiers personnels sont à trier toi-même."),
        Rule(@"Users\*\AppData", LocationKind.UserData, SortYourself, RiskLevel.Caution,
            "Un dossier caché où les applications rangent leurs réglages, leurs caches et parfois des sauvegardes de jeux.",
            "Ne le vide pas en entier. Ouvre Local et Roaming pour voir quelle application prend de la place."),
        Rule(@"Users\*\AppData\Local", LocationKind.Cache, SortYourself, RiskLevel.Caution,
            "Les données des applications propres à ce PC : caches des navigateurs, fichiers temporaires, données de jeux.",
            "Beaucoup de caches ici peuvent être vidés, mais pas tout : ouvre chaque dossier pour savoir à quelle application il appartient."),
        Rule(@"Users\*\AppData\Local\Temp", LocationKind.Cache, CanBeEmptied, RiskLevel.Safe,
            "Les fichiers temporaires de ton compte, laissés par les applications et les installations.",
            "Peut être vidé. Clean s'en chargera dans une prochaine version, en gardant les fichiers encore utilisés."),
        Rule(@"Users\*\AppData\Roaming", LocationKind.UserData, SortYourself, RiskLevel.Caution,
            "Les réglages et profils des applications qui suivent ton compte : navigateurs, Discord, Spotify, jeux…",
            "Contient souvent des données importantes (profils, sauvegardes). Ne supprime un dossier que si l'application est désinstallée."),
        Rule(@"Users\*\AppData\LocalLow", LocationKind.Cache, SortYourself, RiskLevel.Caution,
            "Les données des applications qui tournent avec des droits réduits, souvent des jeux (Unity).",
            "Contient parfois des sauvegardes de jeux : vérifie avant de supprimer."),
        Personal(@"Users\*\Desktop", "Les fichiers de ton Bureau."),
        Personal(@"Users\*\Documents", "Tes documents, et souvent les sauvegardes de jeux ou les fichiers de logiciels."),
        Personal(@"Users\*\Downloads", "Tout ce que tu as téléchargé : installateurs, archives, vidéos… Souvent l'endroit le plus facile à trier."),
        Personal(@"Users\*\Pictures", "Tes images et captures d'écran."),
        Personal(@"Users\*\Videos", "Tes vidéos et enregistrements d'écran."),
        Personal(@"Users\*\Music", "Ta musique."),
        Personal(@"Users\*\OneDrive", "Tes fichiers synchronisés avec OneDrive."),
        Personal(@"Users\*\Saved Games", "Des sauvegardes de jeux."),
        Rule(@"Users\*\.nuget", LocationKind.Cache, Rebuilds, RiskLevel.Caution,
            "Le cache des paquets NuGet, utilisé pour développer en .NET.",
            "Peut être vidé si tu ne développes plus en .NET : les paquets seront retéléchargés au besoin."),
        Rule(@"Users\*\.vscode", LocationKind.Applications, SortYourself, RiskLevel.Caution,
            "Les extensions et réglages de Visual Studio Code.",
            "Désinstalle les extensions inutiles depuis VS Code plutôt que de supprimer le dossier."),
        Rule(@"Users\*\.cache", LocationKind.Cache, Rebuilds, RiskLevel.Caution,
            "Un cache utilisé par des outils de développement.",
            "Généralement recréé au besoin, mais vérifie à quel outil il appartient avant de le vider."),

        // Installed applications and their shared data
        Rule(@"Program Files\Common Files", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Des composants partagés entre plusieurs applications.",
            SystemAdvice),
        Rule(@"Program Files\WindowsApps", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les applications installées depuis le Microsoft Store.",
            "Désinstalle-les depuis Paramètres › Applications."),
        Rule(@"Program Files (x86)\Common Files", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Des composants partagés entre plusieurs applications 32 bits.",
            SystemAdvice),
        Rule(@"Program Files\*", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les fichiers de l'application ou de l'éditeur « {name} ».",
            UninstallAdvice),
        Rule(@"Program Files (x86)\*", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les fichiers de l'application ou de l'éditeur « {name} » (version 32 bits).",
            UninstallAdvice),
        Rule(@"ProgramData\Package Cache", LocationKind.Cache, DoNotTouch, RiskLevel.Blocked,
            "Des copies d'installateurs (Visual Studio, .NET, redistribuables…) gardées pour réparer ou désinstaller ces logiciels.",
            "Ne le vide pas à la main : ces logiciels pourraient devenir impossibles à mettre à jour ou à désinstaller."),
        Rule(@"ProgramData\Microsoft", LocationKind.System, DoNotTouch, RiskLevel.Blocked,
            "Les données de Windows et des logiciels Microsoft : Defender, recherche, diagnostics…",
            SystemAdvice),
        Rule(@"ProgramData\*", LocationKind.Applications, DoNotTouch, RiskLevel.Blocked,
            "Les données partagées de « {name} » : réglages, licences, caches.",
            "Désinstaller « {name} » supprime en général aussi ces données. Ne vide pas le dossier à la main."),
        Rule(@"$Recycle.Bin\*", LocationKind.RecycleBin, CanBeEmptied, RiskLevel.Safe,
            "La corbeille d'un des comptes du PC.",
            "Vide la Corbeille depuis le Bureau si tu n'as plus rien à récupérer."),

        // Game launchers, usually on a secondary drive
        .. SteamLibrary("SteamLibrary"),
        .. SteamLibrary(@"Steam"),
        .. SteamLibrary(@"Program Files (x86)\Steam"),
        .. SteamLibrary(@"Program Files\Steam"),
        .. GameLibrary("Epic Games", "Epic Games"),
        .. GameLibrary(@"Program Files\Epic Games", "Epic Games"),
        .. GameLibrary("XboxGames", "Xbox"),
        .. GameLibrary("Riot Games", "Riot Games"),
    ];

    // Folders recognised by their name wherever they are, mostly development tools.
    public static IReadOnlyDictionary<string, LocationDescription> AnywhereByName { get; } =
        new Dictionary<string, LocationDescription>(StringComparer.OrdinalIgnoreCase)
        {
            ["node_modules"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Caution,
                "Les dépendances d'un projet JavaScript, téléchargées par npm, pnpm ou yarn.",
                "Peut être supprimé si tu ne travailles plus sur ce projet : la commande « npm install » le recrée entièrement."),
            [".git"] = Describe(LocationKind.UserData, DoNotTouch, RiskLevel.Blocked,
                "L'historique Git d'un projet : toutes ses versions et ses branches.",
                "Ne le supprime pas : tu perdrais tout l'historique du projet."),
            [".venv"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Caution,
                "L'environnement virtuel Python d'un projet (ses bibliothèques installées).",
                "Peut être supprimé si le projet n'est plus utilisé : il se recrée avec « pip install »."),
            ["venv"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Caution,
                "L'environnement virtuel Python d'un projet (ses bibliothèques installées).",
                "Peut être supprimé si le projet n'est plus utilisé : il se recrée avec « pip install »."),
            ["__pycache__"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Safe,
                "Des fichiers Python précompilés.",
                "Peut être supprimé sans risque : Python le recrée automatiquement."),
            [".vs"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Caution,
                "Les fichiers de travail de Visual Studio pour ce projet (index, caches).",
                "Peut être supprimé quand Visual Studio est fermé : il sera recréé à la prochaine ouverture."),
            [".idea"] = Describe(LocationKind.Cache, SortYourself, RiskLevel.Caution,
                "Les réglages d'un projet ouvert avec un éditeur JetBrains (Rider, IntelliJ, WebStorm…).",
                "Petit en général. Le supprimer réinitialise les réglages du projet dans l'éditeur."),
            [".gradle"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Caution,
                "Le cache de Gradle, l'outil de compilation des projets Java et Android.",
                "Peut être vidé si tu ne développes plus en Java ou Android : il se retélécharge au besoin."),
            [".next"] = Describe(LocationKind.Cache, Rebuilds, RiskLevel.Safe,
                "Le résultat de compilation d'un site Next.js.",
                "Peut être supprimé sans risque : il est recréé à la prochaine compilation."),
        };

    private static IEnumerable<LocationRule> SteamLibrary(string root) =>
    [
        Rule(root, LocationKind.Applications, SortYourself, RiskLevel.Caution,
            "Une bibliothèque de jeux Steam.",
            "Pour libérer de la place, désinstalle les jeux depuis Steam (clic droit sur le jeu › Gérer › Désinstaller)."),
        Rule($@"{root}\steamapps", LocationKind.Applications, SortYourself, RiskLevel.Caution,
            "Les jeux Steam de cette bibliothèque, leurs mises à jour et leurs contenus additionnels.",
            "Désinstalle les jeux depuis Steam, jamais en supprimant les dossiers : Steam les croirait toujours installés."),
        Rule($@"{root}\steamapps\common", LocationKind.Applications, SortYourself, RiskLevel.Caution,
            "Les dossiers d'installation des jeux Steam : un sous-dossier par jeu.",
            "Ouvre un jeu pour voir sa taille, puis désinstalle-le depuis Steam s'il ne sert plus."),
        Rule($@"{root}\steamapps\common\*", LocationKind.Applications, SortYourself, RiskLevel.Caution,
            "Le jeu Steam « {name} ».",
            "Si tu n'y joues plus, désinstalle-le depuis Steam (clic droit sur le jeu › Gérer › Désinstaller). Tes sauvegardes dans le cloud Steam sont conservées."),
        Rule($@"{root}\steamapps\workshop", LocationKind.Applications, SortYourself, RiskLevel.Caution,
            "Les mods et contenus du Workshop Steam téléchargés pour tes jeux.",
            "Désabonne-toi des contenus inutiles dans le Workshop : Steam supprimera les fichiers."),
        Rule($@"{root}\steamapps\shadercache", LocationKind.Cache, Rebuilds, RiskLevel.Safe,
            "Le cache des shaders précompilés de tes jeux Steam.",
            "Peut être vidé sans risque, mais les jeux peuvent ramer un peu aux premiers lancements le temps de le recréer."),
        Rule($@"{root}\steamapps\downloading", LocationKind.Cache, CanBeEmptied, RiskLevel.Caution,
            "Les téléchargements et mises à jour Steam en cours.",
            "Laisse Steam terminer ou annuler le téléchargement plutôt que de supprimer ces fichiers."),
    ];

    private static IEnumerable<LocationRule> GameLibrary(string root, string launcher) =>
    [
        Rule(root, LocationKind.Applications, SortYourself, RiskLevel.Caution,
            $"Les jeux installés avec {launcher}.",
            $"Ouvre un jeu pour voir sa taille, puis désinstalle-le depuis {launcher} s'il ne sert plus."),
        Rule($@"{root}\*", LocationKind.Applications, SortYourself, RiskLevel.Caution,
            $"Le jeu ou l'application « {{name}} », installé avec {launcher}.",
            $"Si tu n'y joues plus, désinstalle-le depuis {launcher}, pas en supprimant le dossier."),
    ];

    private static LocationDescription Describe(LocationKind kind, string verdict, RiskLevel risk, string summary, string advice) =>
        new(kind, verdict, risk, summary, advice);

    private static LocationRule Rule(string pattern, LocationKind kind, string verdict, RiskLevel risk, string summary, string advice) =>
        new(pattern, new LocationDescription(kind, verdict, risk, summary, advice));

    private static LocationRule Personal(string pattern, string summary) =>
        Rule(pattern, LocationKind.UserData, SortYourself, RiskLevel.Caution, summary, PersonalAdvice);

    private static LocationRule UpdateLeftovers(string pattern) =>
        Rule(pattern, LocationKind.System, ThroughWindows, RiskLevel.Caution,
            "Des fichiers temporaires laissés par une mise à jour ou une installation de Windows.",
            "Une fois la mise à jour terminée, supprime-les avec « Nettoyage de disque » › Nettoyer les fichiers système.");
}
