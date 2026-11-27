using System.ComponentModel.DataAnnotations;
using AmrGrandPrix.API.Common;

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
    /// Earliest possible birth date implied by the ages and age categories in this runner's race
    /// results (null = no lower bound). See <see cref="EstimatedBirthRange"/>.
    /// </summary>
    public DateOnly? EstimatedDobEarliest { get; set; }

    /// <summary>
    /// Latest possible birth date implied by the runner's race results (null = no upper bound).
    /// See <see cref="EstimatedBirthRange"/>.
    /// </summary>
    public DateOnly? EstimatedDobLatest { get; set; }

    /// <summary>
    /// The span of possible birth dates, narrowed by every result saved for this runner (each
    /// result's window is intersected in; a contradicting result is ignored unless an admin
    /// chooses to replace the estimate). Maintained even when <see cref="DateOfBirth"/> is set,
    /// but a verified date of birth always takes precedence when reading an age. Not mapped:
    /// it's a view over <see cref="EstimatedDobEarliest"/>/<see cref="EstimatedDobLatest"/>.
    /// </summary>
    public BirthDateRange EstimatedBirthRange
    {
        get => new(EstimatedDobEarliest, EstimatedDobLatest);
        set
        {
            EstimatedDobEarliest = value.Earliest;
            EstimatedDobLatest = value.Latest;
        }
    }

    /// <summary>
    /// Computed (not stored): the year in the middle of <see cref="EstimatedBirthRange"/>, or null
    /// when the range is wider than one reported age (e.g. only age categories are known).
    /// </summary>
    public int? EstimatedBirthYear => EstimatedBirthRange.EstimatedYear;

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
