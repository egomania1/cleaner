# AUDIT — Clean (H:\clean)

Date : 2026-10-01. Branche auditée : `winui` (commit 885950d), plus un coup d'œil à `main` (ancienne version Electron).
Phase 1, lecture seule : aucun fichier du projet modifié, aucun nettoyage lancé. Seul ce fichier a été créé.
Commandes lancées : `git`, `dotnet list package --vulnerable` (interroge NuGet, n'installe rien), lectures de fichiers.

> **Limites de cet audit, à connaître avant de lire le reste**
> - J'ai lu en entier tout le code qui supprime, déplace, restaure, scanne, lance un programme ou lit le registre. J'ai seulement parcouru les contrôles visuels (`Controls/`, 26 fichiers), les catalogues de textes (`Core/Storage`) et le XAML des pages.
> - Je n'ai pas mesuré la couverture de tests (coverlet est présent mais je ne l'ai pas lancé), ni les contrastes WCAG, ni Lighthouse (il n'y a pas de site web à mesurer).
> - Les points de droit sont une aide à la réflexion, pas un avis juridique. Fais valider CGV, mentions et rétractation par un juriste ou un service spécialisé avant la vente.
> - Les défauts marqués « lu dans le code, non reproduit » n'ont pas été exécutés, pour respecter la lecture seule.

---

## 1. Résumé en 5 lignes

1. **Prête à être vendue ? NON.** Le cœur (analyse, nettoyage restaurable, explications) est sérieux et bien protégé ; tout le reste d'un produit vendu n'existe pas encore.
2. Il n'y a **ni site, ni serveur, ni compte, ni paiement, ni licence, ni page légale, ni installateur, ni signature de code, ni mise à jour**. Ce sont les bloquants.
3. Le code de nettoyage est la partie la plus solide : refus par défaut, liens/junctions testés, archive de 7 jours, aucun accès réseau, aucun secret, aucune dépendance vulnérable dans l'app livrée.
4. Les vrais défauts techniques sont des failles de robustesse (crash possible au démarrage, fichiers archivés sans trace après une erreur, disques réseau proposés), pas des failles « pirate ».
5. Sur les fonctions concurrentielles, 3 sur 10 sont faites ; 3 pages sur 10 de la navigation sont encore des maquettes.

---

## 2. Notes sur 10

| Domaine | Note | Pourquoi |
|---|---|---|
| Sécurité | **6 / 10** | App locale : 7,5 (conception défensive solide, tests de junctions). Distribution : 2 (pas de signature, pas de mise à jour, runtime non embarqué). Partie web/serveur : inexistante, donc non notée. |
| Paiement et licences | **0 / 10** | N'existe pas. |
| Légal France / RGPD | **1 / 10** | Aucune page, aucun texte. Seul point favorable : l'app n'envoie aucune donnée, donc le RGPD est simple. |
| Responsive web | **0 / 10** | Il n'y a pas d'interface web. L'app est une fenêtre WinUI. |
| Qualité du code | **7 / 10** | Architecture propre, 304 tests, 0 avertissement. Défauts : gestion d'erreurs trop étroite, ViewModels non testés, pas de CI. |
| Fonctions vs marché | **5 / 10** | Très bon sur l'explication, la carte du disque et la quarantaine. Absent : démarrage, désinstalleur, Windows 11, maintenance automatique. |

---

## 3. Cartographie

### 3.1 Technologies et dépendances

| Élément | Version | Où |
|---|---|---|
| Langage / runtime | C# 13 (`LangVersion latest`), **.NET 8** | `Directory.Build.props` |
| Interface | WinUI 3, `Microsoft.WindowsAppSDK` **2.5.1**, non empaqueté (`WindowsPackageType=None`) | `Clean.App.csproj:5-14` |
| MVVM | `CommunityToolkit.Mvvm` 8.4.2 | `Clean.App.csproj` |
| Injection | `Microsoft.Extensions.DependencyInjection` / `Logging` / `Logging.Debug` 8.0.1 ; `Logging.Abstractions` 8.0.3 | |
| Outils | `Microsoft.Windows.SDK.BuildTools` 10.0.28000.2705 | |
| Tests | xUnit 2.5.3, `Microsoft.NET.Test.Sdk` 17.8.0, coverlet 6.0.0 | `Clean.Tests.csproj:11-14` |
| Qualité du build | `TreatWarningsAsErrors`, `Nullable` | `Directory.Build.props` |

Volume : ~11 300 lignes de C#, ~3 200 de XAML, ~3 000 de tests (304 tests, tous verts). Aucun `.editorconfig`, aucun analyseur, aucun CI (`.github` absent).

Branche `main` : ancienne version Electron/React (`electron ^43.2.0`, React 19, Vite 6). Elle n'est plus la version suivie.

### 3.2 Architecture

```
Clean.App            WinUI 3 : Views, ViewModels (24), Controls (26), Services
Clean.Core           modèles, règles, moteur de sécurité, formats (net8.0 pur)
Clean.Infrastructure disques, fichiers, registre (lecture), processus, navigateurs, archive
Clean.Tests          xUnit (Core + Infrastructure uniquement)
```

| Partie demandée | État |
|---|---|
| Client PC | **Existe** (Clean.exe, non signé, non installable) |
| Partie web (site, compte, achat) | **N'existe pas** |
| Serveur / API | **N'existe pas** |
| Base de données | **N'existe pas** (seul stockage : `%LOCALAPPDATA%\Clean\Archive\history.json`) |
| Paiement / licence | **N'existe pas** |
| Réseau | **Aucun** : aucun `HttpClient`, `Socket`, URL dans `src/` (seuls les espaces de noms XAML contiennent « http ») |

### 3.3 Ce que l'app fait aujourd'hui

- Tableau de bord (disques, espace récupérable).
- **Nettoyage** : analyse à blanc puis nettoyage réel de 16 règles JSON (temporaires, miniatures, journaux Windows, minidumps, rapports d'erreurs, caches GPU, caches npm/pip/NuGet) et des caches des navigateurs détectés (Chrome, Edge, Brave, Opera, Firefox). Confirmation avant action, bouton « Arrêter ».
- **Historique et restauration** : les fichiers vont dans une archive restaurable 7 jours.
- **Stockage** : graphique polaire, détail de chaque dossier, explications hors ligne (~70 applications, dossiers connus).
- **Applications** : inventaire, treemap de l'espace, applications lancées avec CPU/RAM en direct.
- **Doublons** et **Gros fichiers** (retrait vers l'archive).

Inachevé : pages **Développeur (07)**, **Démarrage (09)**, **Paramètres (10)** = maquettes (`MainWindow.xaml.cs:28-33`). Phases 11 à 38 du plan non faites (`docs/PROGRESS.md`).

---

## 4. Tableau des problèmes

Gravité : **Critique** = bloque la vente · **Élevé** · **Moyen** · **Faible**.
Les sections web, serveur, paiement et légal n'ont aucun fichier à citer : la ligne « Fichier » dit « absent ».

### 4.1 Critiques (bloquants pour la mise en ligne)

| # | Gravité | Fichier:ligne | Problème | Correctif |
|---|---|---|---|---|
| C1 | **Critique** | absent | Aucun système de vente : pas de paiement, de licence, de compte ni de serveur. | Voir §5 (plan) et §4.5. Stripe Checkout + serveur de licences signées. |
| C2 | **Critique** | `Clean.App.csproj:5-14`, `README.md` | Pas de signature de code, pas d'installateur. L'app a besoin du Windows App Runtime installé séparément. Un `.exe` non signé déclenche SmartScreen et souvent Defender. | Empaqueter en **MSIX signé** (ou installateur + runtime embarqué), certificat de signature (EV ou service de signature cloud), publier le hash SHA-256. |
| C3 | **Critique** | absent | Aucune page légale : mentions légales, CGV, confidentialité, cookies, rétractation. Vendre sans CGV ni information précontractuelle expose à des sanctions (DGCCRF). | Voir §4.6. |
| C4 | **Critique** | absent | Aucune mise à jour : impossible de corriger une faille ou une règle dangereuse chez les clients. | Mise à jour via MSIX/App Installer ou MS Store, ou un updater qui vérifie une **signature** (jamais seulement HTTPS). |

### 4.2 Sécurité et robustesse de l'app de nettoyage

| # | Gravité | Fichier:ligne | Problème | Correctif |
|---|---|---|---|---|
| S1 | ~~Élevé~~ **CORRIGÉ** (phase 2, étape 1) | `App.xaml.cs:57-67`, `CleaningArchive.cs:213-219` | `FreeExpiredArchives` est un `async void` qui n'attrape que `IOException`/`UnauthorizedAccessException`. Si `history.json` contient un identifiant de session invalide (fichier abîmé ou modifié à la main), `SessionFolder` lève `ArgumentException`, personne ne l'attrape et l'app plante à **chaque** démarrage. *(lu dans le code, non reproduit)* | Voir code ci-dessous. Attraper toute exception dans cette méthode, la journaliser, ne jamais laisser sortir un `async void`. Valider le fichier à la lecture (`ReadHistoryAsync`) et mettre en quarantaine les sessions invalides. |
| S2 | ~~Élevé~~ **CORRIGÉ** (phase 2, étape 1) | `FileCleaner.cs:34-52`, `FileCleaner.cs:119` | La session n'est enregistrée dans `history.json` qu'**après** le nettoyage. Si une exception autre qu'`IOException`/`UnauthorizedAccessException` survient en cours de route (chemin invalide, `NotSupportedException`…), des fichiers sont déjà déplacés dans l'archive mais **aucune trace** n'existe : impossibles à restaurer ou à libérer depuis l'app. *(non reproduit)* | Enregistrer la session en `try/finally` avec ce qui a été déplacé ; ou écrire la session « en cours » **avant** de déplacer. Voir code. |
| S3 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 1) | `DiskService.cs:17-47`, `FileToolViewModel.cs` | Les lecteurs réseau, amovibles et optiques sont proposés pour Doublons et Gros fichiers, qui peuvent **retirer** des fichiers. Sur un partage réseau, l'archive se crée à la racine du partage (droits inconnus, pas de corbeille, lent). | Ne proposer à l'action de retrait que `DriveType.Fixed` (et amovible après avertissement), jamais `Network`/`CDRom`. |
| S4 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 1) | `FileRemover.cs:138-146` | Le retrait vérifie que **le fichier** n'est pas un lien, mais pas ses dossiers parents. Si un dossier est remplacé par une junction entre l'analyse et le retrait, `MoveTo` suit le lien. (L'analyse, elle, ne suit jamais les liens.) | Réutiliser `IReparsePointDetector.FindLinkOnPath(file.FullName)` comme le fait déjà `PathValidator`. |
| S5 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 1) | `CleaningArchive.cs:128-141` | La restauration remet les fichiers à `location.Path` lu dans `history.json`, sans repasser par `PathValidator`, et sans vérifier qu'aucun parent n'est devenu un lien. Un historique modifié peut rediriger la restauration. | À la restauration : `IsValid` du `PathValidator` pour les emplacements issus de règles, détection de lien sur le chemin cible, et refus des cibles protégées. |
| S6 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 3) | `CleaningArchive.cs:229-234` | Sur les disques secondaires, l'archive `<disque>\.CleanArchive` est créée à la racine, cachée mais avec les droits hérités (souvent lisible par tous les comptes du PC). Elle peut contenir des fichiers personnels retirés (doublons, gros fichiers). | Poser une ACL explicite : propriétaire = utilisateur courant, héritage supprimé, accès refusé aux autres comptes (sauf SYSTEM/Administrateurs). |
| S7 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 3) | `BrowserRuleBuilder.cs`, `BrowserDetector.cs` | Les caches des navigateurs sont nettoyés même si le navigateur tourne. Chromium ouvre ses fichiers de cache en partage de suppression : déplacer un fichier vivant peut abîmer l'index du cache (au pire, cache reconstruit ; pas de perte de données utilisateur). | Détecter le processus du navigateur (nom de l'exécutable) au scan et marquer l'élément « fermer Chrome d'abord » (non coché par défaut). |
| S8 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 3) | `JsonRuleLoader.cs:14`, `RuleValidator.cs`, `ProtectedPathService.cs:6-26` | Les règles sont des JSON **non signés** dans le dossier de l'exe. `RuleValidator` bloque les dossiers protégés, mais `%USERPROFILE%\MesProjets` ou `%TEMP%` redéfini par une variable d'environnement passeraient. Cela suppose déjà qu'un autre programme écrit dans le dossier de l'app ou dans tes variables, donc l'impact reste limité. | Intégrer les règles dans l'assembly (ressources) ou les signer ; refuser une règle dont le chemin résolu sort d'une liste blanche de dossiers de cache connus. |
| S9 | **Moyen** | `app.manifest:1-17` | Aucun niveau d'exécution demandé : l'app tourne **sans droits administrateur**, ce qui est le bon choix de sécurité. Conséquence : `%WINDIR%\Temp`, `Minidump`, `Logs\WindowsUpdate` sont en grande partie « gardés ». À ne **pas** corriger en élevant toute l'app. | Si tu veux ces gains : un petit service/assistant élevé séparé, avec liste blanche stricte de chemins, lancé à la demande (UAC). |
| S10 | **Faible** | `FileExplorer.cs:10-11` | `explorer.exe` est lancé avec le chemin interpolé dans les arguments, sans validation. Aujourd'hui le chemin vient du scan (un nom NTFS ne peut pas contenir `"`), donc non exploitable. | `Path.GetFullPath` + vérifier que le chemin existe, ou utiliser `ProcessStartInfo.ArgumentList`. |
| S11 | ~~~~ **CORRIGÉ** (phase 2, étape 2) | `App.xaml.cs:33-45` | `crash.log` grossit sans limite et contient la trace complète (chemins de fichiers = données personnelles, mais locales). | Rotation (ex. 1 Mo), et prévenir l'utilisateur de ce fichier dans la page confidentialité. |
| S12 | ~~~~ **CORRIGÉ** (phase 2, étape 2) | `Clean.Tests.csproj:11-14` | `dotnet list package --vulnerable` : `System.Net.Http` 4.3.0 et `System.Text.RegularExpressions` 4.3.0 (gravité Haute) en transitif, **dans le projet de tests uniquement**. Rien dans l'app livrée. | Mettre `Microsoft.NET.Test.Sdk` à jour, ou épingler ces deux paquets à une version corrigée. |
| S13 | ~~~~ **CORRIGÉ** (phase 2, étape 2) | `Directory.Build.props`, `*.csproj` | **.NET 8 sort du support Microsoft en novembre 2026** (dans ~6 semaines). Une app vendue sur un runtime sans correctifs de sécurité est un risque. | Migrer vers .NET 10 (LTS) avant la mise en vente. |
| S14 | **Info** | tout `src/` | **Positif** : aucun secret en dur (recherche sur tout le code et l'historique Git : rien), aucun appel PowerShell/cmd (sauf `explorer.exe`), le registre n'est lu qu'en **lecture** (`InstalledProgramCatalog.cs`), aucune injection de commande possible dans le code actuel. | — |

Ce qui est bien conçu et testé, à garder : `PathValidator.cs` (refus des `..`, UNC, `\\?\`, flux alternatifs, racines, dossiers protégés, tout lien sur le chemin), `SafetyEngine` rejoué dans `FileCleaner`, `ReparsePointDetector` testé avec de vraies junctions, âge minimum revérifié au nettoyage, historique écrit de façon atomique, fichiers ouverts gardés, fichiers cloud (OneDrive) jamais déclenchés.

### 4.3 Code à appliquer pour S1, S2, S3, S4 (proposition, rien n'est appliqué)

**S1 — `App.xaml.cs:57-67`**

```csharp
private static async void FreeExpiredArchives()
{
    try
    {
        await Services.GetRequiredService<ICleaningArchive>().FreeExpiredAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
        Services.GetRequiredService<ILogger<App>>().LogError(exception, "Could not free the expired archives");
    }
}
```

Et dans `CleaningArchive.ReadHistoryAsync`, écarter les sessions invalides au lieu de planter :

```csharp
var sessions = await JsonSerializer.DeserializeAsync<List<CleaningSession>>(stream, JsonOptions, cancellationToken) ?? [];
return sessions.Where(IsWellFormed).ToList();

private static bool IsWellFormed(CleaningSession session) =>
    session.Id.Length > 0
    && session.Id.IndexOfAny(['\\', '/', ':', '.']) < 0
    && session.Locations is not null;
```

**S2 — `FileCleaner.CleanAsync`** : enregistrer ce qui a été déplacé même en cas d'erreur.

```csharp
List<CleaningSessionLocation> locations = [];
try
{
    locations = await Task.Run(() => Clean(sessionId, decisions, run, cancellationToken), CancellationToken.None);
}
finally
{
    if (run.RemovedFiles > 0)
    {
        // enregistre la session avec les emplacements déjà traités, puis laisse l'exception remonter
    }
}
```

(Le plus sûr : `Clean` alimente une liste partagée `locations` au fur et à mesure, pour que le `finally` voie ce qui est déjà archivé.)

**S3 — `DiskService.ReadDisks`** : exposer le type et filtrer côté action.

```csharp
public bool CanRemoveFiles => Type == DriveType.Fixed;
```

et dans `FileToolViewModel` ne lister pour le retrait que les disques où `CanRemoveFiles` est vrai.

**S4 — `FileRemover.Refusal`**, après le test de `ReparsePoint` :

```csharp
if (reparsePointDetector.FindLinkOnPath(file.FullName) is { } link)
{
    return $"{link} is a link to another location";
}
```

(injecter `IReparsePointDetector` dans le constructeur de `FileRemover`).

### 4.4 Qualité, robustesse, tests

| # | Gravité | Fichier:ligne | Problème | Correctif |
|---|---|---|---|---|
| Q1 | ~~Moyen~~ **CORRIGÉ** (phase 2, étape 1) | `CleanerViewModel.cs:297,343`, `FileToolViewModel.cs:229,262,311`, `HistoryViewModel.cs:105,164`, `StorageViewModel.cs:260,305`, `AppsViewModel.cs:270,296`, `DashboardViewModel.cs:57` | Partout le même filtre `catch ... when (IOException or UnauthorizedAccessException)`. Toute autre exception (`ArgumentException`, `JsonException`, `InvalidOperationException`, `NullReferenceException`) fait planter l'application pendant un nettoyage ou une analyse. | Un `catch (Exception)` de dernier recours dans chaque commande utilisateur : journaliser, remettre l'état « prêt », afficher « une erreur est survenue, rien n'a été supprimé / voir le journal ». |
| Q2 | **Moyen** | `tests/Clean.Tests.csproj` | Les 24 ViewModels (logique de sélection, confirmation, états) et les 26 contrôles ne sont couverts par **aucun test** : le projet de tests ne référence pas `Clean.App`. Les tests couvrent bien Core/Infrastructure. | Sortir la logique des ViewModels (déjà en partie) ou créer un projet de tests `net8.0-windows` pour les ViewModels sans XAML. |
| Q3 | **Moyen** | `ProcessMonitor.cs` (fenêtres), `InstalledProgramCatalog.cs`, `FileMoveProbe.cs`, `FileExplorer.cs` | Code Windows natif sans test direct (`FileMoveProbe` n'est couvert qu'indirectement par `TempScannerTests`). | Tests d'intégration ciblés (fenêtre factice, verrouillage de fichier réel). |
| Q4 | **Moyen** | absent (`.github`) | Pas de CI : rien n'empêche de livrer avec un test rouge, une dépendance vulnérable, ou sans analyse de sécurité. | GitHub Actions : build, test, `dotnet list package --vulnerable`, CodeQL, couverture. |
| Q5 | ~~~~ **CORRIGÉ** (phase 2, étape 2) | `App.xaml.cs:73` | Journalisation `AddDebug()` uniquement : invisible hors Visual Studio. Sans fichier journal, le support client est impossible. | Journal fichier local (`%LOCALAPPDATA%\Clean\logs`), rotation, **sans chemins personnels complets**, ouvrable depuis Paramètres. |
| Q6 | **Faible** | `FileScanner.cs:17-19` / `TempFilePolicy.cs:10-12` | Mêmes constantes d'attributs cloud définies deux fois ; `EnumerationOptions` répétées 4 fois ; `ReportInterval` répété. | Centraliser dans `Clean.Core.Files`. |
| Q7 | **Faible** | `Clean.Core` | `Clean.Core` appelle `Environment.ExpandEnvironmentVariables` : le README dit « aucune dépendance Windows ». | Injecter la fonction d'expansion partout (c'est déjà le cas dans la plupart des classes). |
| Q8 | **Faible** | racine du dépôt | Pas de fichier `LICENSE` ni de liste des licences tierces (CommunityToolkit MIT, Windows App SDK…). Nécessaire pour vendre. | Ajouter `LICENSE` (propriétaire) et `THIRD-PARTY-NOTICES.md`. |
| Q9 | **Faible** | `Clean-win32-x64.zip` (137 Mo), `node_modules/`, `release/` | Reliquats Electron dans le dossier de travail (ignorés par Git). Risque de les distribuer par erreur. | Supprimer du dossier, ou archiver hors du dépôt. |
| Q10 | **Moyen** | branche `main` : `electron/cleaner.ts:99-103,115-120` | L'ancienne version supprime **définitivement** (`fs.rm recursive force`), sans archive ni restauration, et lance PowerShell. Sa configuration Electron est correcte (`contextIsolation`, `sandbox`, `nodeIntegration:false`, liste blanche des clés côté main). | Ne jamais la vendre. Une fois la version WinUI publiée, supprimer la branche ou la marquer « archive ». |

### 4.5 Paiement et licences (rien n'existe)

| # | Gravité | Fichier:ligne | Problème | Correctif |
|---|---|---|---|---|
| P1 | **Critique** | absent | Pas de paiement. | **Stripe Checkout** (page hébergée par Stripe : tu ne touches jamais un numéro de carte, tu restes hors du périmètre PCI lourd). Portail client Stripe pour résilier, changer d'offre, voir les factures. |
| P2 | **Critique** | absent | Pas de licence. Rien ne distingue un acheteur d'un non-acheteur. | Serveur de licences : après paiement (webhook), émettre un jeton **signé** (Ed25519) lié à un identifiant d'appareil et à une date d'expiration. L'app embarque seulement la **clé publique** et vérifie la signature hors ligne, avec revalidation périodique. La clé privée ne quitte jamais le serveur. |
| P3 | **Élevé** | absent | Webhooks à sécuriser dès le départ. | Vérifier la signature `Stripe-Signature` avec le secret du webhook côté serveur, traiter les événements de façon **idempotente** (même événement reçu deux fois). |
| P4 | **Élevé** | absent | Cas à prévoir : paiement refusé (`invoice.payment_failed`), remboursement (`charge.refunded` → révoquer), abonnement expiré (période de grâce), changement d'offre (proratisation). | Machine d'états de la licence : `active → past_due → grace → expired` / `refunded`. |
| P5 | **Élevé** | absent | Un jeton stocké localement peut être copié, ou l'app patchée pour sauter le test. | On ne peut pas empêcher le piratage d'une app locale : viser la **dissuasion raisonnable** (signature vérifiée à plusieurs endroits, jeton lié à l'appareil, revalidation, binaire signé et non modifiable). Ne pas dégrader l'expérience des clients honnêtes pour lutter contre les pirates. |
| P6 | **Élevé** | absent | Résiliation : l'article **L.215-1-1** que tu cites impose la résiliation en ligne, au même endroit que la souscription. | Bouton « Résilier » dans l'app et sur le site (compte) ; confirmation par e-mail ; effet immédiat ou à échéance, clairement indiqué. À faire valider. |
| P7 | **Moyen** | absent | Prix de renouvellement identique et rappel avant renouvellement : **bonne pratique fortement recommandée** ; l'obligation légale porte surtout sur l'**information claire** de la reconduction tacite. | E-mail de rappel 15 à 30 jours avant, prix de renouvellement affiché dès l'achat. |

### 4.6 Légal France et RGPD (rien n'existe)

| # | Gravité | Problème | Correctif |
|---|---|---|---|
| L1 | **Critique** | Pas de mentions légales (identité de l'éditeur, SIRET, contact, hébergeur). | Page dédiée, lien dans le pied de page et dans l'app. |
| L2 | **Critique** | Pas de CGV ni d'information précontractuelle (prix TTC, durée, reconduction, support, médiateur de la consommation). | Rédiger les CGV ; adhérer à un médiateur de la consommation (obligatoire en B2C). |
| L3 | **Élevé** | **Rétractation de 14 jours** : pour un logiciel téléchargé, elle peut être perdue **seulement si** le client demande expressément l'exécution immédiate **et** reconnaît renoncer à son droit, cases à cocher avant paiement. Sans cela, il faut rembourser sur demande. | Cases explicites au paiement + e-mail de confirmation sur support durable ; procédure de remboursement. |
| L4 | ~~Élevé~~ **CORRIGÉ** (phase 2, étape 1) | Pratiques trompeuses (L.121-2 et suivants). L'app n'invente pas de « problèmes détectés » : bon point. Mais : le bouton **« Supprimer X Go »** (`CleanerViewModel.cs:376`) et `ScanItemRow.cs:59` (« Supprimera X ») alors que les fichiers vont dans l'archive 7 jours et que **l'espace n'est libéré qu'après** (`CleaningSession.cs:15`). L'utilisateur voit que son disque n'a pas bougé. | Reformuler : « Mettre de côté X (libéré sous 7 jours) » **et** proposer « Libérer maintenant » à l'étape de confirmation. Afficher deux chiffres : « mis de côté » et « réellement libéré ». |
| L5 | **Moyen** | Données : **actuellement aucune donnée personnelle ne quitte le PC** (aucun réseau). Dès que tu ajoutes comptes, licences et paiement, tu traites e-mail, identifiant d'appareil, historique d'achat. | Politique de confidentialité : finalités, base légale, durées, hébergement UE, sous-traitants (Stripe, hébergeur, e-mail). Registre des traitements. |
| L6 | **Moyen** | Télémétrie : inexistante aujourd'hui, c'est un **atout commercial** (« 100 % local »). Si tu ajoutes des statistiques ou des rapports d'erreur plus tard, ils doivent être **opt-in** explicites et décrits. | Ne rien envoyer par défaut ; case « envoyer le journal d'erreur » à chaque plantage. |
| L7 | **Moyen** | Droit d'accès/suppression : pour les données du compte (site), prévoir export et suppression du compte. Pour l'app seule, désinstaller + supprimer `%LOCALAPPDATA%\Clean` suffit : à documenter. | Bouton « Supprimer mon compte », page « Données stockées sur ton PC ». |
| L8 | **Moyen** | Cookies : le site (futur) ne doit déposer que des cookies strictement nécessaires sans bandeau ; tout statistique/marketing exige un consentement préalable et refus aussi simple qu'acceptation. | Éviter les traceurs ; sinon CMP conforme CNIL. |
| L9 | **Faible** | Accessibilité : la directive européenne sur l'accessibilité (en vigueur depuis juin 2025) concerne certains services, dont le commerce en ligne ; des micro-entreprises peuvent être exemptées. À vérifier selon ta structure. | Viser WCAG AA de toute façon. |

### 4.7 Web responsive et accessibilité

| # | Gravité | Fichier:ligne | Problème | Correctif |
|---|---|---|---|---|
| W1 | **Critique** | absent | Il n'y a **pas d'interface web**. **Un nettoyeur de PC ne peut pas fonctionner en page web** : un navigateur n'a pas le droit d'analyser ni de supprimer des fichiers du PC. | Deux produits distincts : (1) l'app Windows, qui reste native ; (2) un **site** (vitrine, tarifs, achat, compte, téléchargement, légal), responsive. C'est ce site qui doit passer Lighthouse 90+. Voir question Q1. |
| W2 | ~~Moyen~~ **PARTIEL** (phase 2, étape 3 : vérifié à 960 px, pas à 125/150/200 % ni en grandes polices) | `MainWindow.xaml.cs`, `GlassNavBar` | Dans l'app : la barre de navigation a 10 onglets qui ne tiennent pas dans 960 px (étiquettes visibles seulement pour l'onglet actif, les autres en info-bulles). Fenêtre non testée sur écran étroit, DPI 150-200 %, grandes polices Windows. | Largeur minimale de fenêtre, tests à 125/150/200 %, libellés visibles ou menu compact. |
| W3 | ~~Moyen~~ **PARTIEL** (phase 2, étape 3 : noms des listes et des graphiques ; Narrateur et contrastes non testés) | 5 fichiers XAML sur 17 seulement contiennent `AutomationProperties` ; 4 usages clavier au total (`TabIndex`, `AccessKey`, `KeyboardAccelerator`) | Accessibilité de l'app insuffisante : lecteurs d'écran, navigation au clavier, focus visible non vérifiés sur les contrôles dessinés à la main (treemap, graphiques, boutons « verre »). | `AutomationProperties.Name` sur tout contrôle interactif, ordre de tabulation, équivalent texte des graphiques (la liste triable « table view » existe déjà : bon point), test avec Narrateur, vérifier le contraste (texte atténué à 45 % d'opacité dans le treemap). |
| W4 | **Faible** | `Themes/Theme.xaml` | Un seul thème sombre, pas de mode clair, pas de respect du « contraste élevé » de Windows. | Thème clair + `HighContrast`. |

---

## 5. Comparaison avec le marché

| Fonction | État | Détail |
|---|---|---|
| Mesure avant/après (démarrage, mémoire, disque) avec historique | **Partiel** | Octets retirés par nettoyage (carte « RETIRÉ », page Historique). Pas de temps de démarrage ni de mémoire avant/après ; historique d'évolution du disque = phase 20 non faite. |
| Point de restauration auto, quarantaine 7 jours, « Annuler » | **Partiel (bon)** | Quarantaine 7 jours et « Restaurer » : **oui**. Point de restauration Windows : **non**. |
| Explication de chaque élément + niveau de risque | **Oui** | Description et niveau (sûr / prudence / expert / bloqué) sur chaque règle ; explications hors ligne des applications et dossiers. Un vrai point fort. |
| Programmes au démarrage avec impact en secondes | **Non** | Page 09 = maquette. |
| Carte visuelle du disque, gros fichiers, doublons | **Oui** | Polar, treemap, pages Gros fichiers et Doublons. |
| Désinstalleur qui supprime les restes | **Non** | Le bouton ouvre seulement les Paramètres Windows (`AppsViewModel.cs:72`). |
| Allègement de Windows 11 (réversible) | **Non** | |
| Test de compatibilité Windows 11 | **Non** | |
| Maintenance automatique silencieuse | **Non** | Le champ `automaticCleaningAllowed` existe dans les règles mais rien ne planifie ni n'exécute. |
| Zéro pub, zéro logiciel tiers, pas de nettoyage du registre | **Oui** | Aucun accès réseau, aucune écriture dans le registre. |

Pour battre CCleaner et consorts, ton **différenciateur crédible** est : « 100 % local, tout est expliqué, tout est annulable ». Ne copie pas leurs fonctions agressives (registre, optimisation « boost »).

---

## 6. Plan d'action

### Étape 0 — Décisions à prendre (avant de coder)
Répondre aux questions du §7, surtout Q1 (périmètre web), Q2 (modèle de prix), Q3 (statut juridique).

### Étape 1 — Stabilité de l'app (rapide, bloque une mauvaise première impression)
1. S1 : `async void` + validation de l'historique.
2. S2 : enregistrer la session même en cas d'erreur.
3. Q1 : `catch` de dernier recours dans chaque commande.
4. S3 : ne retirer des fichiers que sur disques fixes.
5. S4 / S5 : liens parents au retrait, validation à la restauration.
6. L4 : reformuler les boutons « Supprimer » ; ajouter « Libérer maintenant ».

### Étape 2 — Bloquants de mise en ligne
7. C2 : MSIX signé + certificat de signature de code.
8. S13 : migration vers .NET 10 LTS.
9. C4 : mécanisme de mise à jour signé.
10. L1-L3 : mentions légales, CGV, médiateur, rétractation.
11. W1/P1/P2 : site (vitrine, tarifs, compte), Stripe Checkout, serveur de licences signées, webhooks.

### Étape 3 — Qualité et confiance
12. Q4 : CI (build, tests, vulnérabilités, CodeQL).
13. Q5 : journal fichier local sans données personnelles.
14. S6, S7, S8 : ACL de l'archive, détection des navigateurs ouverts, règles intégrées/signées.
15. Q2/Q3 : tests des ViewModels et du code natif.
16. W2/W3 : accessibilité de l'app (Narrateur, clavier, contrastes, DPI).
17. Q8-Q10 : LICENSE, mentions tierces, nettoyage du dépôt.

### Étape 4 — Fonctions pour battre le marché
18. Page Démarrage avec impact mesuré (phase 27).
19. Désinstalleur avec détection des restes (dossiers, jamais le registre par défaut).
20. Mesures avant/après (démarrage, mémoire) et historique de l'espace disque (phases 19-20).
21. Point de restauration Windows avant un gros nettoyage.
22. Maintenance planifiée silencieuse, limitée aux règles `safe`, avec rapport.
23. Test de compatibilité Windows 11 ; allègement Windows 11 réversible (en dernier, risque plus élevé).
24. Élévation ciblée (assistant administrateur à liste blanche) si tu veux nettoyer `%WINDIR%` en profondeur.

---

## 7. Questions que je dois te poser

1. **Périmètre web.** Le « responsive web » doit-il être un **site** (vitrine, achat, compte, téléchargement), l'app restant une application Windows ? Une version 100 % web ne pourrait pas nettoyer un PC.
2. **Modèle de vente** : achat unique, abonnement, ou les deux ? Prix visé ? Essai gratuit (fonctions limitées ou durée) ? Combien de PC par licence ?
3. **Statut juridique** : micro-entreprise, société ? SIRET déjà obtenu ? TVA applicable ? (nécessaire pour mentions légales, factures et CGV).
4. **Système cible** : Windows 10 et 11, ou seulement 11 ? Windows 10 n'est plus supporté par Microsoft depuis octobre 2025.
5. **Distribution** : site seul, ou aussi Microsoft Store ? (le Store gère signature et mises à jour, mais impose ses règles et une commission).
6. **Budget signature de code** : certificat EV (~300-500 €/an) ou service de signature cloud ? Sans signature, l'avertissement SmartScreen fera fuir des acheteurs.
7. **Hébergement et serveur** : tu préfères quelle pile (Node, .NET, PHP…) et quel hébergeur UE ? Qui assurera sauvegardes et support ?
8. **Droits administrateur** : veux-tu que l'app nettoie `%WINDIR%` en profondeur (assistant élevé) ou rester sans droits admin ?
9. **Marques** : le nom « Clean » est très générique ; as-tu vérifié sa disponibilité (INPI, domaine, Store) ?
10. **Branche Electron `main`** : peut-elle être archivée et le zip de 137 Mo supprimé ?
11. **Langues** : français seulement au lancement ? (tout le texte est en dur en français dans le code).
12. **Support client** : quelle adresse, quels délais promis dans les CGV ?

---

## 8. PHASE 2

Je n'ai rien corrigé. Dis-moi quelles étapes tu valides (et tes réponses au §7), et je commence par l'**Étape 1** (stabilité), un correctif à la fois, avec explication, test et ton accord avant de continuer.
