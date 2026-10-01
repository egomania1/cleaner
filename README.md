# Clean

Understand your storage. Clean what matters.

Windows desktop app that analyzes what fills a drive, explains it, and only cleans what has been proven safe.
Everything stays on the PC: nothing is sent online.

## What it does

- Explains every item before it is cleaned, with a risk level (safe, caution, expert, blocked).
- Moves cleaned files to a local archive instead of deleting them: restorable for 7 days from the History page.
- Storage map of each disk, installed applications with live CPU and memory, duplicates, large files.
- Browser caches only (Chrome, Edge, Brave, Opera, Firefox): passwords, cookies, sessions and bookmarks are never listed.
- Free analysis. A 5-day full trial, then moving or removing files needs a licence (one-time purchase, sold on a separate website).
  Restoring files is always free.

## Stack

- C# / .NET 10
- WinUI 3 (Windows App SDK 2.5, unpackaged)
- xUnit

## Solution layout

```
Clean.sln
src/
  Clean.App             WinUI 3 user interface
  Clean.Core            Models, interfaces and business rules, no Windows dependency
  Clean.Infrastructure  Windows and file system access
tests/
  Clean.Tests           Unit tests
docs/
  PROGRESS.md           Where the project stands
  LICENSING.md          Licence token contract with the website
AUDIT.md                Security, legal and quality audit with the fixes done so far
```

## Build

Requires the .NET 10 SDK and the Windows App Runtime 2.5 (x64).

```
dotnet restore
dotnet build
dotnet test
```

Run the app:

```
dotnet run --project src/Clean.App
```

The build treats warnings as errors. Continuous integration (`.github/workflows`) builds, runs the tests,
fails on a vulnerable package and runs CodeQL.

## Licence

Proprietary, see `LICENSE`. Third-party components: `THIRD-PARTY-NOTICES.md`.
