using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using VidStash.Helpers;
using VidStash.Models;

namespace VidStash.Services;

public class TmdbService
{
    private readonly HttpClient _http;
    private readonly DatabaseService _db;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBase = "https://image.tmdb.org/t/p/";

    public TmdbService(HttpClient http, DatabaseService db)
    {
        _http = http;
        _db = db;
    }

    private async Task<string?> GetApiKeyAsync() =>
        await _db.GetSettingAsync("tmdb_api_key");

    public static string GetPosterUrl(string? path, string size = "w342") =>
        string.IsNullOrEmpty(path) ? string.Empty : $"{ImageBase}{size}{path}";

    public static string GetBackdropUrl(string? path, string size = "w1280") =>
        string.IsNullOrEmpty(path) ? string.Empty : $"{ImageBase}{size}{path}";

    public static string GetStillUrl(string? path, string size = "w300") =>
        string.IsNullOrEmpty(path) ? string.Empty : $"{ImageBase}{size}{path}";

    public async Task<bool> TestApiKeyAsync(string apiKey)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/configuration?api_key={apiKey}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<TmdbMovie>> SearchMovieAsync(string title, int? year = null)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey)) return [];

        var url = $"{BaseUrl}/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(title)}";
        if (year.HasValue) url += $"&year={year}";

        try
        {
            var result = await _http.GetFromJsonAsync<TmdbSearchResult<TmdbMovie>>(url);
            return result?.Results ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<TmdbTv>> SearchTvAsync(string title, int? year = null)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey)) return [];

        var url = $"{BaseUrl}/search/tv?api_key={apiKey}&query={Uri.EscapeDataString(title)}";
        if (year.HasValue) url += $"&first_air_date_year={year}";

        try
        {
            var result = await _http.GetFromJsonAsync<TmdbSearchResult<TmdbTv>>(url);
            return result?.Results ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<TmdbMovie?> GetMovieDetailsAsync(int id)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            return await _http.GetFromJsonAsync<TmdbMovie>($"{BaseUrl}/movie/{id}?api_key={apiKey}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<TmdbTv?> GetTvDetailsAsync(int id)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            return await _http.GetFromJsonAsync<TmdbTv>($"{BaseUrl}/tv/{id}?api_key={apiKey}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<TmdbSeason?> GetSeasonAsync(int tvId, int seasonNumber)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey)) return null;

        try
        {
            return await _http.GetFromJsonAsync<TmdbSeason>(
                $"{BaseUrl}/tv/{tvId}/season/{seasonNumber}?api_key={apiKey}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TmdbMovie>> GetRecommendationsAsync(int movieId)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey)) return [];

        try
        {
            var result = await _http.GetFromJsonAsync<TmdbRecommendationResult>(
                $"{BaseUrl}/movie/{movieId}/recommendations?api_key={apiKey}");
            return result?.Results ?? [];
        }
        catch
        {
            return [];
        }
    }

    public record ScoredResult(MediaType Type, int TmdbId, string Title, double Score,
        string? PosterPath, string? BackdropPath, double Rating, int VoteCount,
        double Popularity, string? Overview, string? Genres, int? Year,
        int? Runtime, string? Status, int TotalSeasons, int TotalEpisodes);

    public async Task<ScoredResult?> ClassifyAndFetchAsync(ParseResult parsed)
    {
        var movieTask = SearchMovieAsync(parsed.Title, parsed.Year);
        var tvTask = SearchTvAsync(parsed.Title, parsed.Year);

        await Task.WhenAll(movieTask, tvTask);

        var movies = movieTask.Result;
        var tvShows = tvTask.Result;

        double movieScore = 0;
        TmdbMovie? bestMovie = null;
        double tvScore = 0;
        TmdbTv? bestTv = null;

        if (movies.Count > 0)
        {
            bestMovie = movies[0];
            movieScore = ScoreMovie(bestMovie, parsed);
        }

        if (tvShows.Count > 0)
        {
            bestTv = tvShows[0];
            tvScore = ScoreTv(bestTv, parsed);
        }

        // Pattern bonuses
        if (parsed.Type == MediaType.TvEpisode)
            tvScore += 30;
        else
            movieScore += 10;

        if (bestMovie != null)
        {
            double titleSim = StringHelpers.Similarity(parsed.Title, bestMovie.Title);
            double tvTitleSim = bestTv != null ? StringHelpers.Similarity(parsed.Title, bestTv.Name) : 0;
            if (titleSim > 0.95 && tvTitleSim < 0.70)
                movieScore += 20;
        }

        if (movieScore >= tvScore && bestMovie != null)
        {
            if (movieScore < 50) return null;

            var details = await GetMovieDetailsAsync(bestMovie.Id) ?? bestMovie;
            var genres = details.Genres != null
                ? JsonSerializer.Serialize(details.Genres.Select(g => g.Name).ToList())
                : null;
            int? year = null;
            if (!string.IsNullOrEmpty(details.ReleaseDate) && details.ReleaseDate.Length >= 4
                && int.TryParse(details.ReleaseDate[..4], out var y))
                year = y;

            return new ScoredResult(MediaType.Movie, details.Id, details.Title, movieScore,
                details.PosterPath, details.BackdropPath, details.VoteAverage, details.VoteCount,
                details.Popularity, details.Overview, genres, year,
                details.Runtime, null, 0, 0);
        }
        else if (bestTv != null)
        {
            if (tvScore < 50) return null;

            var details = await GetTvDetailsAsync(bestTv.Id) ?? bestTv;
            var genres = details.Genres != null
                ? JsonSerializer.Serialize(details.Genres.Select(g => g.Name).ToList())
                : null;
            int? year = null;
            if (!string.IsNullOrEmpty(details.FirstAirDate) && details.FirstAirDate.Length >= 4
                && int.TryParse(details.FirstAirDate[..4], out var y))
                year = y;

            return new ScoredResult(MediaType.TvEpisode, details.Id, details.Name, tvScore,
                details.PosterPath, details.BackdropPath, details.VoteAverage, details.VoteCount,
                details.Popularity, details.Overview, genres, year,
                null, details.Status, details.NumberOfSeasons, details.NumberOfEpisodes);
        }

        return null;
    }

    private static double ScoreMovie(TmdbMovie m, ParseResult parsed)
    {
        double score = 0;
        score += StringHelpers.Similarity(parsed.Title, m.Title) * 50;
        score += Math.Min(m.Popularity / 50.0, 1.0) * 20;
        score += Math.Min(m.VoteCount / 5000.0, 1.0) * 15;

        if (parsed.Year.HasValue && !string.IsNullOrEmpty(m.ReleaseDate) && m.ReleaseDate.Length >= 4)
        {
            if (int.TryParse(m.ReleaseDate[..4], out var movieYear))
            {
                if (movieYear == parsed.Year) score += 15;
                else if (Math.Abs(movieYear - parsed.Year.Value) <= 1) score += 8;
            }
        }
        return score;
    }

    private static double ScoreTv(TmdbTv t, ParseResult parsed)
    {
        double score = 0;
        score += StringHelpers.Similarity(parsed.Title, t.Name) * 50;
        score += Math.Min(t.Popularity / 50.0, 1.0) * 20;
        score += Math.Min(t.VoteCount / 5000.0, 1.0) * 15;

        if (parsed.Year.HasValue && !string.IsNullOrEmpty(t.FirstAirDate) && t.FirstAirDate.Length >= 4)
        {
            if (int.TryParse(t.FirstAirDate[..4], out var tvYear))
            {
                if (tvYear == parsed.Year) score += 15;
                else if (Math.Abs(tvYear - parsed.Year.Value) <= 1) score += 8;
            }
        }
        return score;
    }
}
