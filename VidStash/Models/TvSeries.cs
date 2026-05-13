using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace VidStash.Models;

[Table("TV_Series")]
public class TvSeries : INotifyPropertyChanged
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Year { get; set; }
    public string? Folder { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public double Rating { get; set; }
    public string? Genres { get; set; }
    public string? Overview { get; set; }
    public int? TmdbId { get; set; }
    public string? Status { get; set; }
    public int TotalSeasons { get; set; }
    public int TotalEpisodes { get; set; }
    public string? AddedAt { get; set; }

    private bool _watched;
    public bool Watched
    {
        get => _watched;
        set
        {
            if (_watched != value)
            {
                _watched = value;
                OnPropertyChanged();
            }
        }
    }

    public List<TvEpisode> Episodes { get; set; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
