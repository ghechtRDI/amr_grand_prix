using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmrGrandPrix.API.Models;

public class GrandPrixPoints
{
    [Key]
    public Guid PointsId { get; set; }

    [Required]
    public Guid RunnerId { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    [Required]
    public Guid ResultId { get; set; }

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
    /// Points earned for this race
    /// </summary>
    [Required]
    public int Points { get; set; }

    /// <summary>
    /// Whether these points include a record bonus (+10 for Open Division)
    /// </summary>
    public bool IsRecordBonus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("RunnerId")]
    public virtual Runner Runner { get; set; } = null!;

    [ForeignKey("RaceId")]
    public virtual Race Race { get; set; } = null!;

    [ForeignKey("ResultId")]
    public virtual RaceResult Result { get; set; } = null!;
}
