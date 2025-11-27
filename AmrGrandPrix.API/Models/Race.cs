using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

public class Race
{
    [Key]
    public Guid RaceId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public bool IsGrandPrixRace { get; set; }

    /// <summary>
    /// Order in the Grand Prix series (1-9 for GP races, null for non-GP races)
    /// </summary>
    public int? GrandPrixRaceOrder { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public int Year { get; set; }

    /// <summary>
    /// Course variant (e.g., "Full Monty", "Uphill Only", "Dome/Original")
    /// </summary>
    [MaxLength(100)]
    public string? CourseVariant { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    /// <summary>
    /// Record time for male division
    /// </summary>
    public TimeSpan? RecordTimeMale { get; set; }

    /// <summary>
    /// Record time for female division
    /// </summary>
    public TimeSpan? RecordTimeFemale { get; set; }

    [MaxLength(200)]
    public string? RecordHolderMale { get; set; }

    [MaxLength(200)]
    public string? RecordHolderFemale { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the person who created this race
    /// </summary>
    public string? CreatedBy { get; set; }

    // Navigation properties
    public virtual ICollection<RaceResult> Results { get; set; } = new List<RaceResult>();
    public virtual ICollection<UploadBatch> UploadBatches { get; set; } = new List<UploadBatch>();
}
