using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidStash.Models;

[Table("Movies")]
public class Movie
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Year { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Folder { get; set; }
    public string? Filename { get; set; }
    public long Size { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public double Rating { get; set; }
    public string? Genres { get; set; }
    public string? Overview { get; set; }
    public int? TmdbId { get; set; }
    public int? Runtime { get; set; }
    public string? AddedAt { get; set; }
    public string? LastPlayed { get; set; }
    public int PlayCount { get; set; }
    public bool Watched { get; set; }
}
