using Microsoft.EntityFrameworkCore;
using VidStash.Models;

namespace VidStash.Services;

public class VidStashDbContext : DbContext
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<TvSeries> TvSeries => Set<TvSeries>();
    public DbSet<TvEpisode> TvEpisodes => Set<TvEpisode>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Setting> Settings => Set<Setting>();

    private readonly string _dbPath;

    public VidStashDbContext()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "VidStash");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "vidstash.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>()
            .HasIndex(m => new { m.Folder, m.Title, m.Year });

        modelBuilder.Entity<Movie>()
            .HasIndex(m => m.Path).IsUnique();

        modelBuilder.Entity<TvSeries>()
            .HasIndex(s => s.Title);

        modelBuilder.Entity<TvEpisode>()
            .HasIndex(e => new { e.SeriesId, e.Season });

        modelBuilder.Entity<TvEpisode>()
            .HasIndex(e => e.Path).IsUnique();

        modelBuilder.Entity<Folder>()
            .HasIndex(f => f.Path).IsUnique();
    }
}

public class DatabaseService
{
    private readonly VidStashDbContext _db;

    public DatabaseService(VidStashDbContext db)
    {
        _db = db;
        _db.Database.EnsureCreated();
    }

    // Movies
    public async Task<List<Movie>> GetMoviesAsync() =>
        await _db.Movies.OrderBy(m => m.Title).ToListAsync();

    public async Task<List<Movie>> GetMoviesByFolderAsync(string folder) =>
        await _db.Movies.Where(m => m.Folder == folder).OrderBy(m => m.Title).ToListAsync();

    public async Task<Movie?> GetMovieByPathAsync(string path) =>
        await _db.Movies.FirstOrDefaultAsync(m => m.Path == path);

    public async Task AddMovieAsync(Movie movie)
    {
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateMovieAsync(Movie movie)
    {
        _db.Movies.Update(movie);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteMovieAsync(string id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie != null)
        {
            _db.Movies.Remove(movie);
            await _db.SaveChangesAsync();
        }
    }

    // TV Series
    public async Task<List<TvSeries>> GetSeriesAsync() =>
        await _db.TvSeries.OrderBy(s => s.Title).ToListAsync();

    public async Task<TvSeries?> GetSeriesByTmdbIdAsync(int tmdbId) =>
        await _db.TvSeries.FirstOrDefaultAsync(s => s.TmdbId == tmdbId);

    public async Task<TvSeries?> GetSeriesByTitleAsync(string title) =>
        await _db.TvSeries.FirstOrDefaultAsync(s => s.Title == title);

    public async Task AddSeriesAsync(TvSeries series)
    {
        _db.TvSeries.Add(series);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateSeriesAsync(TvSeries series)
    {
        _db.TvSeries.Update(series);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSeriesAsync(string id)
    {
        var series = await _db.TvSeries.Include(s => s.Episodes).FirstOrDefaultAsync(s => s.Id == id);
        if (series != null)
        {
            _db.TvEpisodes.RemoveRange(series.Episodes);
            _db.TvSeries.Remove(series);
            await _db.SaveChangesAsync();
        }
    }

    // Episodes
    public async Task<List<TvEpisode>> GetEpisodesAsync(string seriesId) =>
        await _db.TvEpisodes.Where(e => e.SeriesId == seriesId)
            .OrderBy(e => e.Season).ThenBy(e => e.Episode).ToListAsync();

    public async Task<TvEpisode?> GetEpisodeByPathAsync(string path) =>
        await _db.TvEpisodes.FirstOrDefaultAsync(e => e.Path == path);

    public async Task AddEpisodeAsync(TvEpisode episode)
    {
        _db.TvEpisodes.Add(episode);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateEpisodeAsync(TvEpisode episode)
    {
        _db.TvEpisodes.Update(episode);
        await _db.SaveChangesAsync();
    }

    // Folders
    public async Task<List<Folder>> GetFoldersAsync() =>
        await _db.Folders.OrderBy(f => f.Name).ToListAsync();

    public async Task<Folder?> GetFolderByPathAsync(string path) =>
        await _db.Folders.FirstOrDefaultAsync(f => f.Path == path);

    public async Task AddFolderAsync(Folder folder)
    {
        _db.Folders.Add(folder);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateFolderAsync(Folder folder)
    {
        _db.Folders.Update(folder);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteFolderAsync(int id)
    {
        var folder = await _db.Folders.FindAsync(id);
        if (folder != null)
        {
            _db.Folders.Remove(folder);
            await _db.SaveChangesAsync();
        }
    }

    // Settings
    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _db.Settings.FindAsync(key);
        return setting?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        var setting = await _db.Settings.FindAsync(key);
        if (setting == null)
        {
            _db.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
            _db.Settings.Update(setting);
        }
        await _db.SaveChangesAsync();
    }

    // Unwatched
    public async Task<List<Movie>> GetUnwatchedMoviesAsync() =>
        await _db.Movies.Where(m => !m.Watched).OrderBy(m => m.Title).ToListAsync();

    public async Task<List<TvSeries>> GetUnwatchedSeriesAsync() =>
        await _db.TvSeries.Where(s => !s.Watched).OrderBy(s => s.Title).ToListAsync();
}
