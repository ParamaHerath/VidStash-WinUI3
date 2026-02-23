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

    private List<Movie> _allMovies = [];
    private List<TvSeries> _allSeries = [];
    private CancellationTokenSource? _scanCts;

    public LibraryViewModel(DatabaseService db, ScannerService scanner, TmdbService tmdb)
    {
        _db = db;
        _scanner = scanner;
        _tmdb = tmdb;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            var apiKey = await _db.GetSettingAsync("tmdb_api_key");
            HasNoApiKey = string.IsNullOrEmpty(apiKey);

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
        Folders = new ObservableCollection<Folder>(folders);
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
        AvailableGenres = new ObservableCollection<string>(genres.OrderBy(g => g));
    }

    public void ApplyFilters()
    {
        var filtered = _allMovies.AsEnumerable();

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

        Movies = new ObservableCollection<Movie>(filtered);
    }

    public void ApplySeriesFilters()
    {
        var filtered = _allSeries.AsEnumerable();

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

        Series = new ObservableCollection<TvSeries>(filtered);
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

        await ScanFolderAsync(folder.Path);
    }

    [RelayCommand]
    private async Task ScanFolderAsync(string folderPath)
    {
        if (IsScanning) return;

        IsScanning = true;
        _scanCts = new CancellationTokenSource();

        var progress = new Progress<ScannerService.ScanProgress>(p =>
        {
            ScanProgressText = $"Scanning {p.Current}/{p.Total}: {p.CurrentFile}";
        });

        try
        {
            await _scanner.ScanFolderAsync(folderPath, progress, _scanCts.Token);
            await LoadMoviesAsync();
            await LoadSeriesAsync();
            ExtractGenres();
        }
        finally
        {
            IsScanning = false;
            ScanProgressText = string.Empty;
            _scanCts = null;
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

    [RelayCommand]
    private async Task RemoveFolderAsync(Folder folder)
    {
        await _db.DeleteFolderAsync(folder.Id);
        await LoadFoldersAsync();
    }

    [RelayCommand]
    private async Task ToggleWatchedMovieAsync(Movie movie)
    {
        movie.Watched = !movie.Watched;
        await _db.UpdateMovieAsync(movie);
        ApplyFilters();
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
        ApplySeriesFilters();
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
