# Third-party components

Clean is built with the components below. Each keeps its own licence. This list was written from the
project files on 2026-10-01 and must be re-checked (licence text and version) before every release.

## Shipped with the application

| Component | Version | Licence |
|---|---|---|
| .NET runtime and libraries | 10.0 | MIT |
| Microsoft.WindowsAppSDK (WinUI 3) | 2.5.1 | Microsoft Software License Terms, see the package |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | Microsoft Software License Terms, see the package |
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | MIT |
| Microsoft.Extensions.Logging | 10.0.0 | MIT |
| Microsoft.Extensions.Logging.Debug | 10.0.0 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.0 | MIT |

The condensed title font "Bahnschrift" is the Windows system font and is not redistributed.

## Used for tests and builds only (not shipped)

| Component | Version | Licence |
|---|---|---|
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| coverlet.collector | 6.0.4 | MIT |

## To do before the first release

- Copy the full licence text of each shipped component into this file or into a `licenses` folder
  delivered with the installer.
- Confirm the licence of every image, icon and animation idea reused from other sites
  (the app icon `Assets/Clean.ico` and the visual inspirations listed in `docs/PROGRESS.md`).
