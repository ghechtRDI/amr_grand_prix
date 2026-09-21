using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// Groups every running of the same physical event across years and course variants
/// (e.g. all "Mount Marathon Race" instances) so they can be browsed and compared together.
/// </summary>
public class RaceSeries
{
    [Key]
    public Guid RaceSeriesId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Race> Races { get; set; } = new List<Race>();
}
