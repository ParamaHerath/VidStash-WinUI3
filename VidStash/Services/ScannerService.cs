using System.Text.Json;
using VidStash.Helpers;
using VidStash.Models;

namespace VidStash.Services;

public class ScannerService
{
    private readonly ParserService _parser;
    private readonly TmdbService _tmdb;
    private readonly DatabaseService _db;
    private readonly ImageCacheService _imageCache;

    public ScannerService(ParserService parser, TmdbService tmdb, DatabaseService db, ImageCacheService imageCache)
    {
        _parser = parser;
        _tmdb = tmdb;
        _db = db;
        _imageCache = imageCache;
    }

    public record ScanProgress(int Current, int Total, string CurrentFile);

    public async Task ScanFolderAsync(string folderPath, IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var files = FileHelpers.ScanForVideoFiles(folderPath).ToList();
        int total = files.Count;

        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];
            progress?.Report(new ScanProgress(i + 1, total, Path.GetFileName(file)));

            try
            {
                await ProcessFileAsync(file, folderPath);
            }
            catch
            {
                // Skip files that fail to process
            }
        }

        // Update folder last scanned
        var folder = await _db.GetFolderByPathAsync(folderPath);
        if (folder != null)
        {
            folder.LastScanned = DateTime.UtcNow.ToString("o");
            await _db.UpdateFolderAsync(folder);
        }
    }

    private async Task ProcessFileAsync(string filePath, string folderPath)
    {
        // Check if already in database
        var existingMovie = await _db.GetMovieByPathAsync(filePath);
        if (existingMovie != null) return;

        var existingEpisode = await _db.GetEpisodeByPathAsync(filePath);
        if (existingEpisode != null) return;

        var parsed = _parser.Parse(filePath);
        var tmdbResult = await _tmdb.ClassifyAndFetchAsync(parsed);

        var fileInfo = new FileInfo(filePath);

        if (tmdbResult != null)
        {
            if (tmdbResult.Type == MediaType.Movie)
            {
                await SaveMovieAsync(filePath, folderPath, fileInfo, parsed, tmdbResult);
            }
            else
            {
                await SaveTvEpisodeAsync(filePath, folderPath, fileInfo, parsed, tmdbResult);
            }
        }
        else
        {
            // Save as unmatched movie with parsed info
            var movie = new Movie
            {
                Title = parsed.Title,
                Year = parsed.Year?.ToString(),
                Path = filePath,
                Folder = folderPath,
                Filename = Path.GetFileName(filePath),
                Size = fileInfo.Length,
                AddedAt = DateTime.UtcNow.ToString("o")
            };
            await _db.AddMovieAsync(movie);
        }
    }

    private async Task SaveMovieAsync(string filePath, string folderPath, FileInfo fileInfo,
        ParseResult parsed, TmdbService.ScoredResult tmdb)
    {
        // Cache poster
        string? posterUrl = TmdbService.GetPosterUrl(tmdb.PosterPath);
        if (!string.IsNullOrEmpty(posterUrl))
            await _imageCache.GetCachedImageAsync(posterUrl);

        var movie = new Movie
        {
            Title = tmdb.Title,
            Year = tmdb.Year?.ToString(),
            Path = filePath,
            Folder = folderPath,
            Filename = Path.GetFileName(filePath),
            Size = fileInfo.Length,
            Poster = tmdb.PosterPath,
            Backdrop = tmdb.BackdropPath,
            Rating = tmdb.Rating,
            Genres = tmdb.Genres,
            Overview = tmdb.Overview,
            TmdbId = tmdb.TmdbId,
            Runtime = tmdb.Runtime,
            AddedAt = DateTime.UtcNow.ToString("o")
        };

        await _db.AddMovieAsync(movie);
    }

    private async Task SaveTvEpisodeAsync(string filePath, string folderPath, FileInfo fileInfo,
        ParseResult parsed, TmdbService.ScoredResult tmdb)
    {
        // Find or create series
        var series = await _db.GetSeriesByTmdbIdAsync(tmdb.TmdbId);
        if (series == null)
        {
            // Cache poster
            string? posterUrl = TmdbService.GetPosterUrl(tmdb.PosterPath);
            if (!string.IsNullOrEmpty(posterUrl))
                await _imageCache.GetCachedImageAsync(posterUrl);

            series = new TvSeries
            {
                Title = tmdb.Title,
                Year = tmdb.Year?.ToString(),
                Folder = folderPath,
                Poster = tmdb.PosterPath,
                Backdrop = tmdb.BackdropPath,
                Rating = tmdb.Rating,
                Genres = tmdb.Genres,
                Overview = tmdb.Overview,
                TmdbId = tmdb.TmdbId,
                Status = tmdb.Status,
                TotalSeasons = tmdb.TotalSeasons,
                TotalEpisodes = tmdb.TotalEpisodes,
                AddedAt = DateTime.UtcNow.ToString("o")
            };
            await _db.AddSeriesAsync(series);
        }

        // Try to get episode details from TMDB
        string? episodeTitle = parsed.EpisodeTitle;
        string? episodeOverview = null;
        string? stillImage = null;
        int? episodeRuntime = null;
        string? airDate = null;

        if (parsed.Season.HasValue && parsed.Episode.HasValue && tmdb.TmdbId > 0)
        {
            var seasonData = await _tmdb.GetSeasonAsync(tmdb.TmdbId, parsed.Season.Value);
            var epData = seasonData?.Episodes?.FirstOrDefault(e => e.EpisodeNumber == parsed.Episode.Value);
            if (epData != null)
            {
                episodeTitle = epData.Name;
                episodeOverview = epData.Overview;
                stillImage = epData.StillPath;
                episodeRuntime = epData.Runtime;
                airDate = epData.AirDate;
            }
        }

        var episode = new TvEpisode
        {
            SeriesId = series.Id,
            Season = parsed.Season ?? 0,
            Episode = parsed.Episode ?? 0,
            Title = episodeTitle,
            Path = filePath,
            Folder = folderPath,
            Filename = Path.GetFileName(filePath),
            Size = fileInfo.Length,
            Overview = episodeOverview,
            StillImage = stillImage,
            Runtime = episodeRuntime,
            AirDate = airDate,
            AddedAt = DateTime.UtcNow.ToString("o")
        };

        await _db.AddEpisodeAsync(episode);
    }
}
