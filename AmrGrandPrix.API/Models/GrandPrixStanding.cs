using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmrGrandPrix.API.Models;

public class GrandPrixStanding
{
    [Key]
    public Guid StandingId { get; set; }

    [Required]
    public Guid RunnerId { get; set; }

    [Required]
    public int Year { get; set; }

    [Required]
    public Division Division { get; set; }

    /// <summary>
    /// Age category (e.g., "30-39", "17 and Under")
    /// Null for Open Division
    /// </summary>
    [MaxLength(50)]
    public string? AgeCategory { get; set; }

    /// <summary>
    /// Total points (sum of best 4 races)
    /// </summary>
    [Required]
    public int TotalPoints { get; set; }

    /// <summary>
    /// Total number of races completed
    /// </summary>
    [Required]
    public int RacesCompleted { get; set; }

    /// <summary>
    /// Number of races counted toward total (up to 4)
    /// </summary>
    [Required]
    public int RacesCounted { get; set; }

    /// <summary>
    /// Points from best race (for tiebreaker)
    /// </summary>
    public int BestRacePoints { get; set; }

    /// <summary>
    /// Points from second best race (for tiebreaker)
    /// </summary>
    public int SecondBestRacePoints { get; set; }

    /// <summary>
    /// Points from third best race (for tiebreaker)
    /// </summary>
    public int ThirdBestRacePoints { get; set; }

    /// <summary>
    /// Points from fourth best race (for tiebreaker)
    /// </summary>
    public int FourthBestRacePoints { get; set; }

    /// <summary>
    /// Whether runner has completed 7+ Grand Prix races (Run the Gamut)
    /// </summary>
    public bool RunTheGamutQualified { get; set; }

    /// <summary>
    /// Current rank in this division
    /// </summary>
    [Required]
    public int Rank { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("RunnerId")]
    public virtual Runner Runner { get; set; } = null!;
}
