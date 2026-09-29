using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// One course within a <see cref="RaceSeries"/> that is run year after year — e.g. Knoya's
/// "Full Monty", "Dome" and "Happy Trails", or Mount Marathon's "Adult" and "Junior" races.
/// Each yearly running of a variant is a <see cref="Race"/>, so results, records and statistics
/// line up across years. A series with a single course has one variant named
/// <see cref="StandardName"/>.
/// </summary>
public class RaceVariant
{
    public const string StandardName = "Standard";

    [Key]
    public Guid RaceVariantId { get; set; }

    [Required]
    public Guid RaceSeriesId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Other names this course has gone by or appears as in results files (e.g. "Original" for
    /// Knoya's "Dome"). Matched case-insensitively when routing uploaded sections to a variant.
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether a new yearly <see cref="Race"/> of this variant counts toward the Grand Prix unless
    /// overridden on the race (e.g. Knoya moves the GP from the Full Monty to the Dome in a snowy year).
    /// </summary>
    public bool IsGrandPrixByDefault { get; set; }

    /// <summary>Sort order within the series (lowest first).</summary>
    public int DisplayOrder { get; set; }

    public TimeSpan? RecordTimeMale { get; set; }

    public TimeSpan? RecordTimeFemale { get; set; }

    [MaxLength(200)]
    public string? RecordHolderMale { get; set; }

    [MaxLength(200)]
    public string? RecordHolderFemale { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual RaceSeries RaceSeries { get; set; } = null!;
    public virtual ICollection<Race> Races { get; set; } = new List<Race>();

    /// <summary>Whether <paramref name="label"/> is this variant's name or one of its aliases.</summary>
    public bool Matches(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;
        var trimmed = label.Trim();
        return string.Equals(Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
               Aliases.Any(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
