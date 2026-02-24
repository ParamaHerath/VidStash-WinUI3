using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidStash.Models;

[Table("TV_Episodes")]
public class TvEpisode
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ForeignKey(nameof(Series))]
    public string SeriesId { get; set; } = string.Empty;

    public int Season { get; set; }
    public int Episode { get; set; }
    public string? Title { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Folder { get; set; }
    public string? Filename { get; set; }
    public long Size { get; set; }
    public string? Overview { get; set; }
    public string? StillImage { get; set; }
    public int? Runtime { get; set; }
    public string? AirDate { get; set; }
    public string? AddedAt { get; set; }
    public string? LastPlayed { get; set; }
    public int PlayCount { get; set; }

    public TvSeries? Series { get; set; }
}
