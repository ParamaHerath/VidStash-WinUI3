using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VidStash.Models;
using VidStash.Services;
using Windows.Storage.Pickers;

namespace VidStash.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly ScannerService _scanner;
    private readonly TmdbService _tmdb;

    [ObservableProperty]
    private ObservableCollection<Movie> _movies = [];

    [ObservableProperty]
    private ObservableCollection<TvSeries> _series = [];

    [ObservableProperty]
    private ObservableCollection<Folder> _folders = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanProgressText = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedSort = "Title";

    [ObservableProperty]
    private string _selectedView = "Movies";

    [ObservableProperty]
    private ObservableCollection<string> _availableGenres = [];

    [ObservableProperty]
    private string? _selectedGenre;

    [ObservableProperty]
    private bool _hasNoFolders;

    [ObservableProperty]
    private bool _hasNoApiKey;

    [ObservableProperty]
    private string? _selectedFolderPath;

    private List<Movie> _allMovies = [];
    private List<TvSeries> _allSeries = [];
    private CancellationTokenSource? _scanCts;
    private Task? _initializeTask;

    public LibraryViewModel(DatabaseService db, ScannerService scanner, TmdbService tmdb)
    {
        _db = db;
        _scanner = scanner;
        _tmdb = tmdb;
    }

    public Task InitializeAsync()
    {
        return _initializeTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        IsLoading = true;
        try
        {
            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] InitializeAsync starting...");

            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] Getting API key...");
            var apiKey = await _db.GetSettingAsync("tmdb_api_key");
            HasNoApiKey = string.IsNullOrEmpty(apiKey);
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] API key: {(string.IsNullOrEmpty(apiKey) ? "missing" : "present")}");

            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] Loading folders...");
            await LoadFoldersAsync();
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] Loaded {Folders.Count} folders");

            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] Loading movies...");
            await LoadMoviesAsync();
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] Loaded {Movies.Count} movies");

            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] Loading series...");
            await LoadSeriesAsync();
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] Loaded {Series.Count} series");

            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] Extracting genres...");
            ExtractGenres();
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] Extracted {AvailableGenres.Count} genres");

            System.Diagnostics.Debug.WriteLine("[LibraryViewModel] InitializeAsync complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] InitializeAsync FAILED: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LibraryViewModel] Stack: {ex.StackTrace}");
            throw;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RefreshLibraryAsync()
    {
        IsLoading = true;
        try
        {
            await LoadFoldersAsync();
            await LoadMoviesAsync();
            await LoadSeriesAsync();
            ExtractGenres();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadFoldersAsync()
    {
        var folders = await _db.GetFoldersAsync();
        Folders.Clear();
        foreach (var folder in folders)
        {
            Folders.Add(folder);
        }
        HasNoFolders = Folders.Count == 0;
    }

    private async Task LoadMoviesAsync()
    {
        _allMovies = await _db.GetMoviesAsync();
        ApplyFilters();
    }

    private async Task LoadSeriesAsync()
    {
        _allSeries = await _db.GetSeriesAsync();
        ApplySeriesFilters();
    }

    private void ExtractGenres()
    {
        var genres = new HashSet<string>();
        foreach (var m in _allMovies)
        {
            if (!string.IsNullOrEmpty(m.Genres))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(m.Genres);
                    if (list != null)
                        foreach (var g in list) genres.Add(g);
                }
                catch { }
            }
        }
        foreach (var s in _allSeries)
        {
            if (!string.IsNullOrEmpty(s.Genres))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(s.Genres);
                    if (list != null)
                        foreach (var g in list) genres.Add(g);
                }
                catch { }
            }
        }
        AvailableGenres.Clear();
        foreach (var genre in genres.OrderBy(g => g))
        {
            AvailableGenres.Add(genre);
        }
    }

    public void ApplyFilters()
    {
        var filtered = _allMovies.AsEnumerable();

        // Folder filter
        if (!string.IsNullOrEmpty(SelectedFolderPath))
        {
            filtered = filtered.Where(m => m.Folder == SelectedFolderPath);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(m =>
                m.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                (m.Year?.Contains(q) ?? false) ||
                (m.Genres?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false));
        }

        if (!string.IsNullOrEmpty(SelectedGenre))
        {
            filtered = filtered.Where(m =>
                m.Genres != null && m.Genres.Contains(SelectedGenre, StringComparison.CurrentCultureIgnoreCase));
        }

        if (SelectedView == "Unwatched")
        {
            filtered = filtered.Where(m => !m.Watched);
        }

        filtered = SelectedSort switch
        {
            "Rating" => filtered.OrderByDescending(m => m.Rating),
            "Year" => filtered.OrderByDescending(m => m.Year),
            "Duration" => filtered.OrderByDescending(m => m.Runtime),
            "Date Added" => filtered.OrderByDescending(m => m.AddedAt),
            _ => filtered.OrderBy(m => m.Title)
        };

        // Update collection in-place to avoid breaking x:Bind
        var newItems = filtered.ToList();
        Movies.Clear();
        foreach (var movie in newItems)
        {
            Movies.Add(movie);
        }
    }

    public void ApplySeriesFilters()
    {
        var filtered = _allSeries.AsEnumerable();

        // Folder filter
        if (!string.IsNullOrEmpty(SelectedFolderPath))
        {
            filtered = filtered.Where(s => s.Folder == SelectedFolderPath);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(s =>
                s.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                (s.Year?.Contains(q) ?? false));
        }

        if (!string.IsNullOrEmpty(SelectedGenre))
        {
            filtered = filtered.Where(s =>
                s.Genres != null && s.Genres.Contains(SelectedGenre, StringComparison.CurrentCultureIgnoreCase));
        }

        if (SelectedView == "Unwatched")
        {
            filtered = filtered.Where(s => !s.Watched);
        }

        filtered = SelectedSort switch
        {
            "Rating" => filtered.OrderByDescending(s => s.Rating),
            "Year" => filtered.OrderByDescending(s => s.Year),
            "Date Added" => filtered.OrderByDescending(s => s.AddedAt),
            _ => filtered.OrderBy(s => s.Title)
        };

        // Update collection in-place to avoid breaking x:Bind
        var newItems = filtered.ToList();
        Series.Clear();
        foreach (var series in newItems)
        {
            Series.Add(series);
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilters();
        ApplySeriesFilters();
    }

    partial void OnSelectedSortChanged(string value)
    {
        ApplyFilters();
        ApplySeriesFilters();
    }

    partial void OnSelectedGenreChanged(string? value)
    {
        ApplyFilters();
        ApplySeriesFilters();
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;

        var existing = await _db.GetFolderByPathAsync(folder.Path);
        if (existing != null) return;

        var newFolder = new Folder
        {
            Path = folder.Path,
            Name = folder.DisplayName,
            AddedAt = DateTime.UtcNow.ToString("o")
        };
        await _db.AddFolderAsync(newFolder);
        await LoadFoldersAsync();

        // Refresh folder list in NavigationView
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.RefreshFolders();
        }

        await ScanFolderAsync(folder.Path);
    }

    [RelayCommand]
    private async Task ScanFolderAsync(string folderPath)
    {
        if (IsScanning) return;

        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        var cts = _scanCts;

        var progress = new Progress<ScannerService.ScanProgress>(p =>
        {
            ScanProgressText = $"Scanning {p.Current}/{p.Total}: {p.CurrentFile}";
        });

        try
        {
            // Run on background thread to keep the UI responsive
            await Task.Run(() => _scanner.ScanFolderAsync(folderPath, progress, cts.Token), cts.Token);
            await LoadMoviesAsync();
            await LoadSeriesAsync();
            ExtractGenres();
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            ScanProgressText = string.Empty;
            cts.Dispose();
            if (_scanCts == cts) _scanCts = null;
        }
    }

    [RelayCommand]
    private async Task RescanAllFoldersAsync()
    {
        foreach (var folder in Folders)
        {
            await ScanFolderAsync(folder.Path);
        }
    }

    public async Task<(int moviesBefore, int seriesBefore, int moviesAfter, int seriesAfter)> StartupScanAsync()
    {
        if (Folders.Count == 0) return (0, 0, 0, 0);

        var moviesBefore = _allMovies.Count;
        var seriesBefore = _allSeries.Count;

        foreach (var folder in Folders.ToList())
        {
            await ScanFolderAsync(folder.Path);
        }

        return (moviesBefore, seriesBefore, _allMovies.Count, _allSeries.Count);
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(Folder folder)
    {
        await _db.DeleteMoviesByFolderAsync(folder.Path);
        await _db.DeleteEpisodesByFolderAsync(folder.Path);
        await _db.DeleteOrphanedSeriesAsync();
        await _db.DeleteFolderAsync(folder.Id);

        await LoadFoldersAsync();
        await LoadMoviesAsync();
        await LoadSeriesAsync();
        ExtractGenres();

        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.RefreshFolders();
        }
    }

    [RelayCommand]
    private async Task ToggleWatchedMovieAsync(Movie movie)
    {
        movie.Watched = !movie.Watched;
        await _db.UpdateMovieAsync(movie);
        if (SelectedView == "Unwatched")
        {
            Movies.Remove(movie);
        }
    }

    [RelayCommand]
    private async Task DeleteMovieAsync(Movie movie)
    {
        await _db.DeleteMovieAsync(movie.Id);
        _allMovies.Remove(movie);
        ApplyFilters();
    }

    [RelayCommand]
    private async Task ToggleWatchedSeriesAsync(TvSeries series)
    {
        series.Watched = !series.Watched;
        await _db.UpdateSeriesAsync(series);
        if (SelectedView == "Unwatched")
        {
            Series.Remove(series);
        }
    }

    [RelayCommand]
    private async Task DeleteSeriesAsync(TvSeries series)
    {
        await _db.DeleteSeriesAsync(series.Id);
        _allSeries.Remove(series);
        ApplySeriesFilters();
    }

    [RelayCommand]
    private async Task PlayMovieAsync(Movie movie)
    {
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(movie.Path);
            await Windows.System.Launcher.LaunchFileAsync(file);
            movie.PlayCount++;
            movie.LastPlayed = DateTime.UtcNow.ToString("o");
            await _db.UpdateMovieAsync(movie);
        }
        catch { }
    }

    public void CancelScan() => _scanCts?.Cancel();
}
