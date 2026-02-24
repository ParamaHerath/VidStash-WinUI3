using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VidStash.Models;

[Table("Settings")]
public class Setting
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
