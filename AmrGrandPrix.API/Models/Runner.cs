using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

public class Runner
{
    [Key]
    public Guid RunnerId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Verified date of birth: synced from the runner's own profile when they claim this record, or
    /// entered by an admin in Racer Management — the app never estimates or auto-populates it.
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Estimated birth year, derived from an age reported in a race result assuming that age was
    /// accurate as of the race date. Used only when <see cref="DateOfBirth"/> is not set, and
    /// overwritten whenever an admin corrects a runner's age during results review.
    /// </summary>
    public int? EstimatedBirthYear { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    /// <summary>
    /// Denormalized from the linked <see cref="ApplicationUser"/>'s profile when a claim is
    /// approved (or set directly by an admin for runners with no linked account). Used as
    /// additional name candidates by <see cref="Services.ResultsProcessing.RunnerMatchingService"/>.
    /// </summary>
    [MaxLength(100)]
    public string? PreferredName { get; set; }

    public List<string> AlternateNames { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Computed property for full name
    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual ICollection<RaceResult> Results { get; set; } = new List<RaceResult>();
    public virtual ICollection<GrandPrixPoints> GrandPrixPoints { get; set; } = new List<GrandPrixPoints>();
    public virtual ICollection<GrandPrixStanding> GrandPrixStandings { get; set; } = new List<GrandPrixStanding>();
}
