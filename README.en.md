# Cloud Game

A lightweight local game library and launcher for Windows. Built with native **WPF / C#** — no Electron, WebView, or third-party runtime.

Refactored on **.NET 8 WPF** with zero third-party UI/MVVM dependencies while retaining the existing pages, layout, native Windows features, and local data format.

Current version: **2.1.0**.

[中文说明](README.md)

![Cloud Game library](docs/screenshots/library-dark.png)

## Features

- **Local library** — add `.exe` / `.bat` / `.cmd` files, or drag them straight into the window.
- **Playtime tracking** — sessions, total playtime, last played, launch count, and live “running” indicators. The detail page includes a current-month play calendar with accent-color intensity per day and month navigation.
- **Custom covers** — choose an image for each game, or let Cloud Game generate procedural cover art.
- **Launcher background** — pick any image in Settings and adjust its dim level.
- **Themes** — dark / light modes and six accent colors, with animated transitions.
- **Import / export** — export play data only, or a full backup including covers, as a single JSON file.
- **Tray & shortcuts** — close to tray, then use the tray menu to open the app, add a game, open Settings, or quit. `Ctrl+F` focuses search; `Esc` closes details/dialogs.
- **Motion** — cover FLIP into details, page fades, list reflow, filter transitions, sidebar thumb, toast, and theme transitions.
- **Elevated games** — automatically retries through UAC when a game requires administrator rights.

![Game details](docs/screenshots/game-detail.png)

## Requirements

- Windows 10 / 11 x64
- .NET 8 Windows Desktop Runtime x64 (included with the .NET 8 SDK on development machines)
- The framework-dependent package needs no application install; run `CloudGame.exe`

## Data location

Everything is stored locally:

```
%APPDATA%\CloudGame\
├── library.json      # games, play sessions, settings
├── media\            # covers and launcher background
└── error.log         # created only when an exception occurs
```

The import/export format is JSON with `format` set to `cloudgame.library`. The refactor preserves the current `library.json` / `media\` paths and `DataContract` schema. Data from the old LUDENS version is not migrated automatically.

## Usage

1. Click **Add game**, or drag an `.exe` into the window.
2. Enter a name and executable; optionally choose a cover (leave empty to auto-generate one).
3. Click a card for details, or click the play button on the cover/right side of a list row to launch directly. Playtime is recorded automatically.
4. Use **Settings** to switch theme/accent, set a launcher background, or import/export data.

### Shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+F` | Open the library and focus search |
| `Esc` | Close details, editor, or confirmation dialog |

## Screenshots

| Dark | Light |
| --- | --- |
| ![Dark library](docs/screenshots/library-dark.png) | ![Light library](docs/screenshots/library-light.png) |

| Statistics | Settings |
| --- | --- |
| ![Statistics](docs/screenshots/stats.png) | ![Settings](docs/screenshots/settings-dark.png) |

| Add game | List view |
| --- | --- |
| ![Game editor](docs/screenshots/editor.png) | ![List view](docs/screenshots/list-view.png) |

## Build from source

The project is an SDK-style .NET 8 WPF project. After installing the .NET 8 SDK, run this from the repository root:

```powershell
dotnet restore .\Ludens.csproj -r win-x64
dotnet build .\Ludens.csproj -c Release --no-restore
dotnet publish .\Ludens.csproj -c Release -r win-x64 --self-contained false `
  -o .\publish\win-x64 --no-restore
```

Output: `publish\win-x64\CloudGame.exe`

### Data compatibility check

The check only reads `%APPDATA%\CloudGame\library.json` and validates .NET 8 deserialization/round-trip serialization. It does not write user data:

```powershell
dotnet run --project .\tests\CloudGame.CompatibilityChecks\CloudGame.CompatibilityChecks.csproj -c Release
```

## Project layout

```
Ludens.csproj           WPF project
App.xaml / App.xaml.cs  startup, themes, crash logging
MainWindow.xaml         window shell, sidebar, page switching, overlays
Views/                  Library / Detail / Stats / Settings / GameEditor / Confirm
Src/                    AppStore, LauncherService, MediaService, ThemeManager,
                        Motion, CoverArt, models, zero-dependency MVVM infrastructure
Src/ViewModels/         ShellViewModel (window commands; page logic migrates incrementally)
tests/                  .NET 8 compatibility check for existing library.json
Resources/Styles.xaml   colors, typography, buttons, inputs, scrollbars, menus
Assets/                 icon and brand assets
docs/screenshots/       documentation screenshots
```

## Notes

- Playtime uses Windows Job Objects to track the process tree. If a game launches through a proxy process that exits immediately, timing may be inaccurate.
- Generated covers are procedural artwork and contain no copyrighted source material.
- Cloud sync is not included; all data stays on the local machine.
