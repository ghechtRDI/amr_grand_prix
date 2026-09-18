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
    /// Self-reported date of birth. Set only when the runner has created and verified their own
    /// profile — the app never estimates or auto-populates this field.
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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Computed property for full name
    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual ICollection<RaceResult> Results { get; set; } = new List<RaceResult>();
    public virtual ICollection<GrandPrixPoints> GrandPrixPoints { get; set; } = new List<GrandPrixPoints>();
    public virtual ICollection<GrandPrixStanding> GrandPrixStandings { get; set; } = new List<GrandPrixStanding>();
}
