# VidStash-WinUI3

A native Windows desktop application built with **WinUI 3** that provides a Netflix-style interface for managing local video libraries. VidStash automatically identifies movies and TV series from filenames, fetches metadata from [TMDB](https://www.themoviedb.org/), and displays everything in a beautiful, fluid poster grid.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue)
![.NET 8](https://img.shields.io/badge/.NET-8-purple)
![WinUI 3](https://img.shields.io/badge/WinUI-3-green)

---

## Features

- **Intelligent Filename Parsing** — Extracts clean titles, years, season/episode info from messy real-world filenames (e.g., `The.Matrix.1999.1080p.BluRay.x264-RARBG.mkv` → *The Matrix (1999)*)
- **TMDB Integration** — Automatically fetches posters, backdrops, ratings, genres, and overviews
- **Adaptive Poster Grid** — Responsive Netflix-style layout with hover effects and watched badges
- **TV Series Support** — Full season/episode management with TMDB episode metadata
- **MVVM Architecture** — Clean separation of concerns using CommunityToolkit.Mvvm
- **Fluent Design** — Mica backdrop, Acrylic materials, system accent colors, dark theme
- **SQLite Database** — Local persistent storage with Entity Framework Core
- **Folder Scanning** — Recursive scan with progress reporting
- **Manual TMDB Search** — Override incorrect matches via a search dialog
- **Watched Tracking** — Mark movies and episodes as watched with visual indicators
- **Context Menus** — Play, mark watched, refresh metadata, delete
- **Image Caching** — Poster and backdrop images cached locally for offline use

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | WinUI 3 (Windows App SDK 1.8) |
| Language | C# / .NET 8 |
| UI | XAML + Fluent Design |
| Database | SQLite + Entity Framework Core 8 |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| Metadata | TMDB API v3 |
| DI | Microsoft.Extensions.DependencyInjection |

## Project Structure

```
VidStash/
├── App.xaml / App.xaml.cs          # Application entry, DI container
├── MainWindow.xaml / .cs           # NavigationView shell with Mica backdrop
├── Models/
│   ├── Movie.cs                    # Movie entity
│   ├── TvSeries.cs                 # TV series entity
│   ├── TvEpisode.cs                # TV episode entity
│   ├── Folder.cs                   # Scanned folder entity
│   ├── Setting.cs                  # Key-value settings entity
│   ├── ParseResult.cs              # Filename parser output
│   └── TmdbModels.cs               # TMDB API response models
├── ViewModels/
│   ├── LibraryViewModel.cs         # Main grid, search, sort, scan
│   ├── MovieDetailViewModel.cs     # Movie detail, recommendations
│   ├── SeriesDetailViewModel.cs    # Series detail, episodes
│   └── SettingsViewModel.cs        # API key, theme, cache
├── Views/
│   ├── LibraryPage.xaml / .cs      # Poster grid for movies and series
│   ├── MovieDetailPage.xaml / .cs  # Movie detail with backdrop
│   ├── SeriesDetailPage.xaml / .cs # Series detail with episodes
│   └── SettingsPage.xaml / .cs     # Settings management
├── Services/
│   ├── ParserService.cs            # ⭐ Intelligent filename parsing
│   ├── DatabaseService.cs          # SQLite + EF Core operations
│   ├── TmdbService.cs              # TMDB API client + scoring
│   ├── ScannerService.cs           # Folder scanning + processing
│   └── ImageCacheService.cs        # Image download + caching
└── Helpers/
    ├── StringHelpers.cs            # Title case, string similarity
    ├── FileHelpers.cs              # Video file detection, size formatting
    └── Converters.cs               # XAML value converters
```

## Getting Started

### Prerequisites

- **Windows 10 version 1809** or later (Windows 11 recommended)
- **Visual Studio 2022 17.8+** with:
  - .NET Desktop Development workload
  - Windows App SDK C# Templates
- **.NET 8 SDK**
- **TMDB API Key** — [Register for free](https://www.themoviedb.org/settings/api)

### Build & Run

1. Clone the repository:
   ```bash
   git clone https://github.com/ParamaHerath/VidStash-WinUI3.git
   cd VidStash-WinUI3
   ```

2. Open `VidStash.sln` in Visual Studio.

3. Set the build platform to **x64** (or x86/ARM64).

4. Press **F5** to build and run.

5. On first launch:
   - Go to **Settings** (gear icon in the sidebar).
   - Enter your **TMDB API key** and click **Save & Test**.
   - Click **Add Folder** to add a video library folder.
   - VidStash will scan the folder and fetch metadata automatically.

### Supported Video Formats

`.mp4`, `.mkv`, `.avi`, `.mov`, `.wmv`, `.flv`, `.webm`, `.m4v`, `.mpg`, `.mpeg`, `.3gp`, `.ts`, `.vob`, `.divx`

## Usage

| Action | How |
|--------|-----|
| Browse movies | Select **All Movies** in the sidebar |
| Browse series | Select **TV Series** in the sidebar |
| View unwatched | Select **Unwatched** in the sidebar |
| Search | Type in the search box (300ms debounce) |
| Sort | Use the Sort dropdown (Title, Rating, Year, Duration, Date Added) |
| Filter by genre | Use the Genre dropdown |
| View details | Click a poster card |
| Play | Click **Play** on the detail page or right-click → Play |
| Mark watched | Right-click → Mark as Watched |
| Refresh metadata | Right-click → Refresh Metadata |
| Manual TMDB search | Click **Manual Search** on the movie detail page |
| Add folder | Click **+ Add Folder** in the sidebar or Settings |

## System Requirements

| Requirement | Minimum |
|-------------|---------|
| OS | Windows 10 1809+ |
| Runtime | .NET 8 |
| Disk | ~200 MB + image cache |
| Internet | Required for TMDB (optional after initial scan) |

## Attribution

> This product uses the TMDB API but is not endorsed or certified by TMDB.

## License

This project is provided as-is for personal use.