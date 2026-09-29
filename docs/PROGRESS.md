# Clean — where we are

Last update: 2026-09-29. Branch: `winui`. The original Electron app is still on `main`.

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
tests/Clean.Tests         xUnit tests (106 passing)
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
| 6 Rule engine (JSON) | **next** | move `Core/Rules/BuiltInRules.cs` to JSON files, validate, expand env vars |
| 7 – 38 | todo | see the master prompt |

## Extra work done on request (outside the phase order)

- Storage page: clickable polar chart and list, CTA-style detail card with an animated gradient (no icons).
- Detail card shows real data for any entry on any drive: owning application (registry or .exe metadata), file/folder counts, composition, last activity, creation date, clickable subfolders with a back button.
- `LocationCatalog` (Windows, Users, Program Files, Steam/Epic/Xbox/Riot, dev folders) and `AppCatalog` (~70 common apps and games) explain what things are. Everything stays offline.
- Shiny analyze button (rotating border light), rectangular buttons (user dislikes round pills), wider pages (1600px).
- Fixed: layout shifting while scanning, disks listed twice, row clicks not opening the card, installers registered on a whole drive.

## User preferences

- Dark acrylic look from the old Electron app; rectangles with 8px corners, not pills; no emoji/icons on cards; gradients welcome, animated preferred.
- App must stay 100 % local (planned public download website later: needs self-contained publish, code signing or Store, installer).
- Wants full explanations for every file and application, on every drive.

## Pending: design inspiration pass

The user shared these sites to borrow animations, text effects and design ideas from:
composites.archi, 21st.dev/community/themes, reactbits.dev, toptier.relats.com, roiheads.com, awwwards.com sites of the month.

Notes taken so far (not implemented yet):
- composites.archi: huge uppercase typography, intro loader with a % counter and a thin line, fine animated line strands in the background, tiny uppercase corner labels, dark grey palette.
- toptier.relats.com: floating glass pill navigation with a two-tab segmented switch, text revealed word by word on scroll, large centered hero title.
- roiheads.com: condensed massive bold type, purple/yellow accents, 3D letters, numbers that "decrypt" (symbols ~ # % & X scramble before the real value) — good fit for sizes like "2,5 Go".
- reactbits.dev text effects that translate well to WinUI: Split Text, Blur Text, Decrypted/Scrambled Text, Shiny Text, Gradient Text, Scroll Reveal, Count Up.
- Not reviewed yet: 21st.dev themes, awwwards sites of the month.

Next step for design: propose a short list of effects to the user, then implement after approval.
