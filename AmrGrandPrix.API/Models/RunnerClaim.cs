using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// A runner's request to link their user account to a historical <see cref="Models.Runner"/>
/// record, subject to admin review.
/// </summary>
public class RunnerClaim
{
    [Key]
    public Guid ClaimId { get; set; }

    [Required]
    public string ApplicationUserId { get; set; } = string.Empty;

    [Required]
    public Guid RunnerId { get; set; }

    [Required]
    public ClaimStatus Status { get; set; } = ClaimStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// User ID of the admin who approved or rejected this claim. Null for a claim that was
    /// auto-approved because <see cref="MatchConfidence"/> was &gt;= 90% at submission time.
    /// </summary>
    public string? ReviewedByUserId { get; set; }

    /// <summary>
    /// The profile-match confidence (0.0-1.0) computed at submission time
    /// (<see cref="Services.ResultsProcessing.IRunnerMatchingService.FindMatchesForProfileAsync"/>).
    /// Recorded for admin audit even when the claim was auto-approved.
    /// </summary>
    public double? MatchConfidence { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual ApplicationUser ApplicationUser { get; set; } = null!;
    public virtual Runner Runner { get; set; } = null!;
}
