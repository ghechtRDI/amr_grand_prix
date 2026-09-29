using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// Whether a year's Grand Prix is over. While finalized, nothing that would change that year's
/// points or standings is allowed (recalculation, or saving/editing/deleting results of its GP
/// races); an admin un-finalizes the season to make corrections, then finalizes it again.
/// A year with no row is not finalized.
/// </summary>
public class GrandPrixSeason
{
    [Key]
    public int Year { get; set; }

    public bool IsFinalized { get; set; }

    public DateTime? FinalizedAt { get; set; }

    /// <summary>User ID of the person who last finalized the season</summary>
    public string? FinalizedBy { get; set; }
}
