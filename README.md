# Clean

A Windows desktop app that scans a drive, groups what it finds into safe-to-delete vs. review-first categories, and clears what you select.

## Features

- Scans user/system temp folders, Windows Error Reporting, browser caches (Chrome, Edge, Brave, Firefox), GPU shader caches (NVIDIA, AMD), leftover old versions of auto-updating apps (Discord, Slack), Spotify's local cache, and the Recycle Bin
- Flags each item as **SUR** (safe) or **A VERIFIER** (review first) so you're never guessing what a cleanup will affect
- Drive overview with a size-proportional radial chart
- Dark, translucent (Windows 11 acrylic) interface

## Tech stack

- Electron + Vite
- React 19 + TypeScript
- Tailwind CSS v4, shadcn-style component structure

## Getting started

```bash
npm install
npm run dev       # start in development mode
```

## Building

```bash
npm run build      # type-check + build the renderer and main process
npm run package    # produce a standalone Windows build in release/
```

## Security

- The renderer never receives raw filesystem paths to delete — it can only reference items by key from the app's own last scan, which the main process resolves and validates before touching disk
- Drive letters are validated before being used in any filesystem or shell command
- `contextIsolation`, `sandbox`, and disabled `nodeIntegration` are set explicitly; window navigation and new-window creation are blocked
