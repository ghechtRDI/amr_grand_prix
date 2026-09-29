using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// A public submission reporting an inaccurate result or requesting help claiming a result that
/// didn't reach the 80% self-serve claim threshold. Emailed to the club and kept here as an
/// admin-visible audit trail (the same pattern as <see cref="UploadBatch"/>'s LLM audit fields).
/// </summary>
public class ResultReport
{
    [Key]
    public Guid ReportId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RunnerNameReported { get; set; } = string.Empty;

    public DateOnly? DateOfBirthReported { get; set; }

    /// <summary>
    /// The race the report concerns, if the submitter identified one in the system.
    /// </summary>
    public Guid? RaceId { get; set; }

    [MaxLength(200)]
    public string? RaceName { get; set; }

    public DateOnly? RaceDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ReporterEmail { get; set; } = string.Empty;

    [Required]
    public ReportStatus Status { get; set; } = ReportStatus.New;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual Race? Race { get; set; }
}
