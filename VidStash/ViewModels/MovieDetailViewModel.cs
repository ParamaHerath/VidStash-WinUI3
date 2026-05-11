using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VidStash.Models;
using VidStash.Services;

namespace VidStash.ViewModels;

public partial class MovieDetailViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly TmdbService _tmdb;

    [ObservableProperty]
    private Movie? _movie;

    [ObservableProperty]
    private ObservableCollection<TmdbMovie> _recommendations = [];

    [ObservableProperty]
    private ObservableCollection<TmdbMovie> _similarMovies = [];

    [ObservableProperty]
    private List<string> _genres = [];

    [ObservableProperty]
    private bool _isLoading;

    public MovieDetailViewModel(DatabaseService db, TmdbService tmdb)
    {
        _db = db;
        _tmdb = tmdb;
    }

    public async Task LoadAsync(Movie movie)
    {
        Movie = movie;
        IsLoading = true;

        try
        {
            if (!string.IsNullOrEmpty(movie.Genres))
            {
                try { Genres = JsonSerializer.Deserialize<List<string>>(movie.Genres) ?? []; }
                catch { Genres = []; }
            }

            if (movie.TmdbId.HasValue)
            {
                var recsTask = _tmdb.GetRecommendationsAsync(movie.TmdbId.Value);
                var similarTask = _tmdb.GetSimilarAsync(movie.TmdbId.Value);
                await Task.WhenAll(recsTask, similarTask);
                Recommendations = new ObservableCollection<TmdbMovie>(recsTask.Result.Take(10));
                SimilarMovies = new ObservableCollection<TmdbMovie>(similarTask.Result.Take(10));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (Movie == null) return;
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(Movie.Path);
            await Windows.System.Launcher.LaunchFileAsync(file);
            Movie.PlayCount++;
            Movie.LastPlayed = DateTime.UtcNow.ToString("o");
            await _db.UpdateMovieAsync(Movie);
        }
        catch { }
    }

    [RelayCommand]
    private async Task ToggleWatchedAsync()
    {
        if (Movie == null) return;
        Movie.Watched = !Movie.Watched;
        await _db.UpdateMovieAsync(Movie);
        OnPropertyChanged(nameof(Movie));
    }

    [RelayCommand]
    private async Task RefreshMetadataAsync()
    {
        if (Movie?.TmdbId == null) return;
        IsLoading = true;

        try
        {
            var details = await _tmdb.GetMovieDetailsAsync(Movie.TmdbId.Value);
            if (details != null)
            {
                Movie.Title = details.Title;
                Movie.Overview = details.Overview;
                Movie.Poster = details.PosterPath;
                Movie.Backdrop = details.BackdropPath;
                Movie.Rating = details.VoteAverage;
                Movie.Runtime = details.Runtime;
                if (details.Genres != null)
                    Movie.Genres = JsonSerializer.Serialize(details.Genres.Select(g => g.Name).ToList());
                await _db.UpdateMovieAsync(Movie);
                await LoadAsync(Movie);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ManualSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        var results = await _tmdb.SearchMovieAsync(query);
        // Return results for UI to display - handled in code-behind
    }

    public async Task ApplyManualMatchAsync(TmdbMovie match)
    {
        if (Movie == null) return;

        var details = await _tmdb.GetMovieDetailsAsync(match.Id) ?? match;
        Movie.TmdbId = details.Id;
        Movie.Title = details.Title;
        Movie.Overview = details.Overview;
        Movie.Poster = details.PosterPath;
        Movie.Backdrop = details.BackdropPath;
        Movie.Rating = details.VoteAverage;
        Movie.Runtime = details.Runtime;
        if (!string.IsNullOrEmpty(details.ReleaseDate) && details.ReleaseDate.Length >= 4)
            Movie.Year = details.ReleaseDate[..4];
        if (details.Genres != null)
            Movie.Genres = JsonSerializer.Serialize(details.Genres.Select(g => g.Name).ToList());

        await _db.UpdateMovieAsync(Movie);
        await LoadAsync(Movie);
    }
}
