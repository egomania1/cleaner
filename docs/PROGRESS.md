# Clean — where we are

Last update: 2026-09-30. Branch: `winui`. The original Electron app is still on `main`.

## How to work on it

- Stack: C# .NET 8, WinUI 3 (Windows App SDK 2.5.1, unpackaged), MVVM with CommunityToolkit.Mvvm, DI with Microsoft.Extensions.
- Build and test from the repo root: `dotnet build Clean.sln` then `dotnet test Clean.sln`.
- Run: `src/Clean.App/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/Clean.exe`.
- The build must stay at 0 warnings (`TreatWarningsAsErrors`).
- Working method: one phase at a time, build + test + real run, then a "PHASE X COMPLETE" checkpoint. Wait for "go" before the next phase. Explain the code in French at each step.
- Code style: simple and clean, no obvious comments (only non-obvious "why"), small files, no business logic in views.
- Edit source files with UTF-8 aware tools. Windows PowerShell 5.1 `Get-Content` / `WriteAllText` breaks French accents.

## Solution layout

```
src/Clean.Core            models, interfaces, rules, formatting, location/app knowledge (no Windows dependency)
src/Clean.Infrastructure  disks, file system walks, registry, Explorer, scanners
src/Clean.App             WinUI 3 UI: Views, ViewModels, Controls, Themes, Converters, Services
tests/Clean.Tests         xUnit tests (288 passing)
```

## Phases (from the DeepClean master prompt, app name kept as "Clean")

| Phase | Status | Notes |
|---|---|---|
| 0 Environment | done | solution + 4 projects |
| 1 Architecture | done | models, interfaces |
| 2 UI shell | done | navigation, dashboard, LumaSpin loader (x3) at startup |
| 3 Disk service | done | real disks on the dashboard |
| 4 Scan engine | done | ScanManager, StorageAnalyzer, Stockage page with SegmentedProgress + PolarChart |
| 5 Dry run | done | KnownLocationScanner + BuiltInRules (C#), Nettoyage page, dashboard "Nettoyable" card |
| 6 Rule engine (JSON) | done | `Infrastructure/Rules/Definitions/*.json` copied to `Rules/` next to the exe; `JsonRuleLoader` skips and logs broken or unsafe rules; `RuleValidator` (id, texts, risk, env-var-only paths, protected folders); `RuleEngine.FindRuleFor` |
| 7 Safety engine | done | `ProtectedPathService` (shared protected-folder list, also used by `RuleValidator`); `PathValidator` (replaces `RulePathValidator`: deny by default, rejects `.`/`..`, UNC and `\\?\` paths, `/`, alternate streams, drive roots, protected folders, non-rule folders, and any link on the way); `ReparsePointDetector` (Infrastructure) walks every folder from the drive down, so a junctioned *parent* is caught too; `RiskAnalyzer` gives the final SAFE/CAUTION/EXPERT/BLOCKED. Real-junction tests: link inside a rule folder, rule folder itself a junction, junctioned parent with a forged decision. |
| 8 Temp scanner | done | `TempScanner` handles the `temporary` rules (`KnownLocationScanner` the others, split in `App.xaml.cs`). Per file: size, age, attributes, `TempFilePolicy` (keeps system files, cloud placeholders, files newer than the rule's `minimumAgeDays`, and documents/photos/videos/music that may be the only copy of an opened attachment) and `FileMoveProbe` (opens with DELETE access only: in use / reserved to admin; a read open made the antivirus scan every file, 65 s vs 1.6 s on 27k files). `ScanItem.KeptFiles` lists kept files per reason, shown on each Nettoyage card. `FileCleaner` applies the same policy to temporary folders. `RuleFolders` (shared by both scanners) now skips any folder with a link on its path. |
| 9 Windows cleaner | **next** | see the master prompt |
| 10 – 38 | todo | see the master prompt |

## Extra work done on request (outside the phase order)

- Storage page: clickable polar chart and list, CTA-style detail card with an animated gradient (no icons).
- Detail card shows real data for any entry on any drive: owning application (registry or .exe metadata), file/folder counts, composition, last activity, creation date, clickable subfolders with a back button.
- `LocationCatalog` (Windows, Users, Program Files, Steam/Epic/Xbox/Riot, dev folders) and `AppCatalog` (~70 common apps and games) explain what things are. Everything stays offline.
- Shiny analyze button (rotating border light), rectangular buttons (user dislikes round pills), wider pages (1600px).
- Nettoyage page: disk picker (system drive by default); scanners take the drive root and ignore rule folders on other drives. Current rules only target the system drive, so other drives show an explanatory "nothing found" card. Idea: drive-level rules (recycle bin, game shader caches on any drive). The disk list is read once per session (a USB drive plugged in later needs a restart).
- Real cleaning on the Nettoyage page (2026-09-30), simulation mode removed: checkbox per location (safe ones ticked by default; "Éléments sûrs / Tout / Aucun"), "Nettoyer X" button, confirmation dialog ("Annuler" is the default button, caution/expert items and refused items listed), live progress with "Arrêter", "RETIRÉ" result card (locked files, files kept because they changed since the scan, "Voir l'historique"). Safety chain: `RulePathValidator` (only the exact folder of a loaded rule, never a drive root) → `SafetyEngine` (rule id and risk must still match) → `FileCleaner` re-checks every decision, re-applies the minimum age at cleaning time, never follows links, removes only empty subfolders created before the cutoff, never the rule folder itself. `%WINDIR%\Temp` mostly needs admin rights: those files are counted as "gardés".
- History and restore (2026-09-30, user request): `FileCleaner` moves files into `CleaningArchive` (`%LOCALAPPDATA%\Clean\Archive`: `history.json` + `sessions\<id>\<location index>\<relative path>`) instead of deleting them. Each cleaning is a `CleaningSession`, restorable for 7 days (`CleaningSession.RetentionPeriod`); expired sessions are freed at startup. Historique page (08): "RESTAURABLE" / "LIBÉRÉ" totals, one card per cleaning with "Restaurer" and "Libérer maintenant" (confirmed). Restore keeps a file the app recreated meanwhile (conflict), keeps the session restorable if a file could not go back. Trade-off: the disk space only comes back when the archive is freed. Unreadable history is copied to `history.json.broken`.
- Applications page (04, 2026-09-30, user asked for "a real stats dashboard, no basic cards", installed + running): KPI band (installed, disk space, running, CPU, RAM), squarified `TreemapChart` of disk space per app (top 32 + gray "autres", hover tooltip, click = detail), ranked bars per category, two `LiveLineChart`s (CPU %, RAM, 60 points every 1.5 s, crosshair tooltip; one scale each, never dual-axis), running apps list with live CPU/RAM and `Sparkline`, detail panel with `AnimatedGradient` header, per-app live charts, description from `AppGuide`, "Ouvrir le dossier" / "Désinstaller dans Windows" (opens ms-settings), full searchable/sortable list (the table view of the treemap). Core: `AppInventory` (dedupe, shared folders never measured, games nested in a launcher folder excluded from its size, a folder shared by two entries counted once), `UsageCalculator` (processes grouped by installed folder, then by exe description/name for apps registered without folder such as Chrome; Windows and protected processes in their own groups; CPU only from two measures of the same process id + start time), `Treemap.Layout`, `AppGroups` (7 groups, fixed colors validated with the dataviz checker for the dark surface). Infrastructure: `ProcessMonitor` (QueryFullProcessImageName, no admin), `FolderSizer`, registry `EstimatedSize`. Sampling only while the page is shown. Unhandled exceptions are written to `%LOCALAPPDATA%\Clean\crash.log` (one unexplained WinUI crash was seen once while testing with simulated clicks; not reproduced since).
- Doublons (05) and Gros fichiers (06) pages (2026-09-30): both on `FileToolViewModel` (disk + minimum size, analysis, tick files, "Retirer" with confirmation) and the shared `FileToolStatus` control. `FileScanner` walks the user part of a drive only (`UserFileScope`: no Windows, Program Files, ProgramData, AppData, `$Recycle.Bin`, System Volume Information, `.CleanArchive`, drive-root or system-flagged files; links never followed; OneDrive cloud-only files skipped so nothing is downloaded). `DuplicateFinder`: same size → same first/last 64 KB → same full SHA-256; hard links are one file, not duplicates; files inside installed apps' folders are left out. Duplicates page: KPI band, wasted space by type and by folder, keep strategy (oldest / newest / shortest path), one card per group, the last copy can never be ticked. Large files page: KPI band (total, count, biggest, "oubliés" > 1 year), treemap of the 40 biggest with a detail panel (`LocationGuide` explanation), size by age (ordinal blue ramp) and by type, category filter and sort; files of installed apps are shown but cannot be removed. `FileRemover` moves files into the archive (history + restore) after re-checking scope, links, owner app, size/date unchanged, and for duplicates that the kept copy still exists. The archive now lives on the files' own drive (`<drive>\.CleanArchive`, hidden) so removing on D: never fills C:. Chart categories: 8 validated colors (Vidéos, Images, Archives, Documents, Images disque, Programmes, Journaux, Données), the rest folded into gray "Autres fichiers". Verified on screen: top of Gros fichiers with real data. Not verified on screen: Doublons results, Gros fichiers lower half and detail panel (window was minimized by the user).
- Fixed: layout shifting while scanning, disks listed twice, row clicks not opening the card, installers registered on a whole drive.

## User preferences

- Dark acrylic look from the old Electron app; rectangles with 8px corners, not pills; no emoji/icons on cards; gradients welcome, animated preferred.
- App must stay 100 % local (planned public download website later: needs self-contained publish, code signing or Store, installer).
- Wants full explanations for every file and application, on every drive.

## Design pass (done 2026-09-30)

Inspiration: composites.archi, toptier.relats.com, roiheads.com, reactbits.dev.

- `PageTitle`: huge condensed uppercase title (Bahnschrift, 60px) with a tiny "NN  /  CLEAN" label; words rise one by one, then a blue-violet light band (`TextShine`) sweeps across every ~5 s.
- `AnimatedText`: count up (dashboard stat cards, Nettoyage "RÉCUPÉRABLE" + shine) and decrypt (sizes on dashboard, Nettoyage, Stockage, detail card). Frame logic lives in `Core/Formatting/TextEffects.cs` (tested). Only for final values, never live progress.
- `LineStrands`: two ribbons of thin lines drawn once over 2x the width with a periodic wave, slid by a compositor animation (idle CPU 4 % vs 2.7 % before, most of it the ShinyButton spin).
- Startup loader: LumaSpin x3 kept, plus "CLEAN" title, thin fill line and a time-paced 0-100 % counter (`CompleteAsync` before the fade).
- `GlassNavBar` replaces the NavigationView: floating glass bar centered at the top; only the selected tab shows its label (10 labelled tabs do not fit 960px), a glass block slides to it, labels are tooltips otherwise.
- All animations respect the Windows "animation effects" setting.
- Not done from the notes: 3D letters (roiheads), word-by-word scroll reveal, 21st.dev themes and awwwards review.
