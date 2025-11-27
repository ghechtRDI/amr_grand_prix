using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmrGrandPrix.API.Models;

public class RaceResult
{
    [Key]
    public Guid ResultId { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    [Required]
    public Guid RunnerId { get; set; }

    /// <summary>
    /// Bib number (optional)
    /// </summary>
    public int? Bib { get; set; }

    /// <summary>
    /// Overall place (null for DNF/DNS/DQ)
    /// </summary>
    public int? Place { get; set; }

    /// <summary>
    /// Place within gender (null for DNF/DNS/DQ)
    /// </summary>
    public int? PlaceGender { get; set; }

    /// <summary>
    /// Place within age category (null for DNF/DNS/DQ)
    /// </summary>
    public int? PlaceAgeCategory { get; set; }

    /// <summary>
    /// Finish time (null for DNF/DNS/DQ)
    /// </summary>
    public TimeSpan? Time { get; set; }

    /// <summary>
    /// Runner's age at time of race
    /// </summary>
    [Required]
    public int Age { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [Required]
    public ResultStatus Status { get; set; } = ResultStatus.Finished;

    /// <summary>
    /// Additional notes or comments
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this result set a new course record
    /// </summary>
    public bool IsNewRecord { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID of the person who uploaded this result
    /// </summary>
    public string? UploadedBy { get; set; }

    /// <summary>
    /// Groups results from the same upload
    /// </summary>
    [Required]
    public Guid UploadBatchId { get; set; }

    // Navigation properties
    [ForeignKey("RaceId")]
    public virtual Race Race { get; set; } = null!;

    [ForeignKey("RunnerId")]
    public virtual Runner Runner { get; set; } = null!;

    [ForeignKey("UploadBatchId")]
    public virtual UploadBatch UploadBatch { get; set; } = null!;

    public virtual ICollection<GrandPrixPoints> GrandPrixPoints { get; set; } = new List<GrandPrixPoints>();
}
