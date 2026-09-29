using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// A recurring event (e.g. "Mount Marathon Race"). It owns the <see cref="RaceVariant"/>s
/// (courses) that are run each year; every <see cref="Race"/> is one year's running of one variant.
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

    public virtual ICollection<RaceVariant> Variants { get; set; } = new List<RaceVariant>();
}
