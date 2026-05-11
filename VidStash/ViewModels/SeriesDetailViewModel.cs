using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VidStash.Models;
using VidStash.Services;

namespace VidStash.ViewModels;

public partial class SeriesDetailViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly TmdbService _tmdb;

    [ObservableProperty]
    private TvSeries? _series;

    [ObservableProperty]
    private ObservableCollection<TvEpisode> _episodes = [];

    [ObservableProperty]
    private List<int> _seasons = [];

    [ObservableProperty]
    private int _selectedSeason = 1;

    [ObservableProperty]
    private List<string> _genres = [];

    [ObservableProperty]
    private ObservableCollection<TmdbTv> _similarShows = [];

    [ObservableProperty]
    private ObservableCollection<TmdbTv> _recommendations = [];

    [ObservableProperty]
    private bool _isLoading;

    public SeriesDetailViewModel(DatabaseService db, TmdbService tmdb)
    {
        _db = db;
        _tmdb = tmdb;
    }

    public async Task LoadAsync(TvSeries series)
    {
        Series = series;
        IsLoading = true;

        try
        {
            if (!string.IsNullOrEmpty(series.Genres))
            {
                try { Genres = JsonSerializer.Deserialize<List<string>>(series.Genres) ?? []; }
                catch { Genres = []; }
            }

            var allEpisodes = await _db.GetEpisodesAsync(series.Id);
            Seasons = allEpisodes.Select(e => e.Season).Distinct().OrderBy(s => s).ToList();

            if (Seasons.Count > 0)
                SelectedSeason = Seasons[0];

            await LoadEpisodesForSeasonAsync();

            if (series.TmdbId.HasValue)
            {
                var recsTask = _tmdb.GetTvRecommendationsAsync(series.TmdbId.Value);
                var similarTask = _tmdb.GetTvSimilarAsync(series.TmdbId.Value);
                await Task.WhenAll(recsTask, similarTask);
                Recommendations = new ObservableCollection<TmdbTv>(recsTask.Result.Take(10));
                SimilarShows = new ObservableCollection<TmdbTv>(similarTask.Result.Take(10));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedSeasonChanged(int value)
    {
        _ = LoadEpisodesForSeasonAsync();
    }

    private async Task LoadEpisodesForSeasonAsync()
    {
        if (Series == null) return;
        var allEpisodes = await _db.GetEpisodesAsync(Series.Id);
        var filtered = allEpisodes.Where(e => e.Season == SelectedSeason)
            .OrderBy(e => e.Episode).ToList();
        Episodes = new ObservableCollection<TvEpisode>(filtered);
    }

    [RelayCommand]
    private async Task PlayEpisodeAsync(TvEpisode episode)
    {
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(episode.Path);
            await Windows.System.Launcher.LaunchFileAsync(file);
            episode.PlayCount++;
            episode.LastPlayed = DateTime.UtcNow.ToString("o");
            await _db.UpdateEpisodeAsync(episode);
        }
        catch { }
    }

    [RelayCommand]
    private async Task ToggleWatchedAsync()
    {
        if (Series == null) return;
        Series.Watched = !Series.Watched;
        await _db.UpdateSeriesAsync(Series);
        OnPropertyChanged(nameof(Series));
    }

    [RelayCommand]
    private async Task RefreshMetadataAsync()
    {
        if (Series?.TmdbId == null) return;
        IsLoading = true;

        try
        {
            var details = await _tmdb.GetTvDetailsAsync(Series.TmdbId.Value);
            if (details != null)
            {
                Series.Title = details.Name;
                Series.Overview = details.Overview;
                Series.Poster = details.PosterPath;
                Series.Backdrop = details.BackdropPath;
                Series.Rating = details.VoteAverage;
                Series.Status = details.Status;
                Series.TotalSeasons = details.NumberOfSeasons;
                Series.TotalEpisodes = details.NumberOfEpisodes;
                if (details.Genres != null)
                    Series.Genres = JsonSerializer.Serialize(details.Genres.Select(g => g.Name).ToList());
                await _db.UpdateSeriesAsync(Series);
                await LoadAsync(Series);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ApplyManualMatchAsync(TmdbTv match)
    {
        if (Series == null) return;

        var details = await _tmdb.GetTvDetailsAsync(match.Id) ?? match;
        Series.TmdbId = details.Id;
        Series.Title = details.Name;
        Series.Overview = details.Overview;
        Series.Poster = details.PosterPath;
        Series.Backdrop = details.BackdropPath;
        Series.Rating = details.VoteAverage;
        Series.Status = details.Status;
        Series.TotalSeasons = details.NumberOfSeasons;
        Series.TotalEpisodes = details.NumberOfEpisodes;
        if (!string.IsNullOrEmpty(details.FirstAirDate) && details.FirstAirDate.Length >= 4)
            Series.Year = details.FirstAirDate[..4];
        if (details.Genres != null)
            Series.Genres = JsonSerializer.Serialize(details.Genres.Select(g => g.Name).ToList());

        await _db.UpdateSeriesAsync(Series);
        await LoadAsync(Series);
    }
}
