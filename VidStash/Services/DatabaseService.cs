using Microsoft.EntityFrameworkCore;
using VidStash.Models;

namespace VidStash.Services;

public class VidStashDbContext : DbContext
{
    public VidStashDbContext(DbContextOptions<VidStashDbContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<TvSeries> TvSeries => Set<TvSeries>();
    public DbSet<TvEpisode> TvEpisodes => Set<TvEpisode>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Setting> Settings => Set<Setting>();

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
    private readonly IDbContextFactory<VidStashDbContext> _factory;

    public DatabaseService(IDbContextFactory<VidStashDbContext> factory)
    {
        _factory = factory;
    }

    // Movies
    public async Task<List<Movie>> GetMoviesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Movies.OrderBy(m => m.Title).ToListAsync();
    }

    public async Task<List<Movie>> GetMoviesByFolderAsync(string folder)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Movies.Where(m => m.Folder == folder).OrderBy(m => m.Title).ToListAsync();
    }

    public async Task<Movie?> GetMovieByPathAsync(string path)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Path == path);
    }

    public async Task AddMovieAsync(Movie movie)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
    }

    public async Task UpdateMovieAsync(Movie movie)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Movies.Update(movie);
        await db.SaveChangesAsync();
    }

    public async Task DeleteMovieAsync(string id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var movie = await db.Movies.FindAsync(id);
        if (movie != null)
        {
            db.Movies.Remove(movie);
            await db.SaveChangesAsync();
        }
    }

    // TV Series
    public async Task<List<TvSeries>> GetSeriesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.TvSeries.OrderBy(s => s.Title).ToListAsync();
    }

    public async Task<TvSeries?> GetSeriesByTmdbIdAsync(int tmdbId)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.TvSeries.AsNoTracking().FirstOrDefaultAsync(s => s.TmdbId == tmdbId);
    }

    public async Task<TvSeries?> GetSeriesByTitleAsync(string title)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.TvSeries.AsNoTracking().FirstOrDefaultAsync(s => s.Title == title);
    }

    public async Task AddSeriesAsync(TvSeries series)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.TvSeries.Add(series);
        await db.SaveChangesAsync();
    }

    public async Task UpdateSeriesAsync(TvSeries series)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.TvSeries.Update(series);
        await db.SaveChangesAsync();
    }

    public async Task DeleteSeriesAsync(string id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var series = await db.TvSeries.Include(s => s.Episodes).FirstOrDefaultAsync(s => s.Id == id);
        if (series != null)
        {
            db.TvEpisodes.RemoveRange(series.Episodes);
            db.TvSeries.Remove(series);
            await db.SaveChangesAsync();
        }
    }

    // Episodes
    public async Task<List<TvEpisode>> GetEpisodesAsync(string seriesId)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.TvEpisodes.Where(e => e.SeriesId == seriesId)
            .OrderBy(e => e.Season).ThenBy(e => e.Episode).ToListAsync();
    }

    public async Task<TvEpisode?> GetEpisodeByPathAsync(string path)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.TvEpisodes.AsNoTracking().FirstOrDefaultAsync(e => e.Path == path);
    }

    public async Task AddEpisodeAsync(TvEpisode episode)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.TvEpisodes.Add(episode);
        await db.SaveChangesAsync();
    }

    public async Task UpdateEpisodeAsync(TvEpisode episode)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.TvEpisodes.Update(episode);
        await db.SaveChangesAsync();
    }

    // Folders
    public async Task<List<Folder>> GetFoldersAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Folders.OrderBy(f => f.Name).ToListAsync();
    }

    public async Task<Folder?> GetFolderByPathAsync(string path)
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Path == path);
    }

    public async Task AddFolderAsync(Folder folder)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Folders.Add(folder);
        await db.SaveChangesAsync();
    }

    public async Task UpdateFolderAsync(Folder folder)
    {
        using var db = await _factory.CreateDbContextAsync();
        db.Folders.Update(folder);
        await db.SaveChangesAsync();
    }

    public async Task DeleteFolderAsync(int id)
    {
        using var db = await _factory.CreateDbContextAsync();
        var folder = await db.Folders.FindAsync(id);
        if (folder != null)
        {
            db.Folders.Remove(folder);
            await db.SaveChangesAsync();
        }
    }

    // Settings
    public async Task<string?> GetSettingAsync(string key)
    {
        using var db = await _factory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(key);
        return setting?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        using var db = await _factory.CreateDbContextAsync();
        var setting = await db.Settings.FindAsync(key);
        if (setting == null)
        {
            db.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
            db.Settings.Update(setting);
        }
        await db.SaveChangesAsync();
    }

    // Unwatched
    public async Task<List<Movie>> GetUnwatchedMoviesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.Movies.Where(m => !m.Watched).OrderBy(m => m.Title).ToListAsync();
    }

    public async Task<List<TvSeries>> GetUnwatchedSeriesAsync()
    {
        using var db = await _factory.CreateDbContextAsync();
        return await db.TvSeries.Where(s => !s.Watched).OrderBy(s => s.Title).ToListAsync();
    }
}

