namespace VidStash.Models;

public enum MediaType
{
    Movie,
    TvEpisode,
    Unknown
}

public class ParseResult
{
    public MediaType Type { get; set; } = MediaType.Unknown;
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public string? EpisodeTitle { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
}
