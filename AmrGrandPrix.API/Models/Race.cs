using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// One year's running of a <see cref="RaceVariant"/>. The race's name and series come from its
/// variant (<c>RaceVariant.RaceSeries.Name</c>, <c>RaceVariant.Name</c>).
/// </summary>
public class Race
{
    [Key]
    public Guid RaceId { get; set; }

    [Required]
    public Guid RaceVariantId { get; set; }

    /// <summary>
    /// Whether this running counts toward the Grand Prix. Defaults from
    /// <see cref="RaceVariant.IsGrandPrixByDefault"/> but can differ in a given year.
    /// </summary>
    public bool IsGrandPrixRace { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public int Year { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the person who created this race
    /// </summary>
    public string? CreatedBy { get; set; }

    // Navigation properties
    public virtual RaceVariant RaceVariant { get; set; } = null!;
    public virtual ICollection<RaceResult> Results { get; set; } = new List<RaceResult>();
    public virtual ICollection<UploadBatch> UploadBatches { get; set; } = new List<UploadBatch>();
}
