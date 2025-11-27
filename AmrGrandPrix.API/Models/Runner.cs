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
    /// Date of birth (optional, used for age verification)
    /// </summary>
    public DateTime? DateOfBirth { get; set; }

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
