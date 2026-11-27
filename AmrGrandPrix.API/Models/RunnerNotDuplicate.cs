using System.ComponentModel.DataAnnotations;

namespace AmrGrandPrix.API.Models;

/// <summary>
/// An admin's ruling that two runner records are different people, so the pair is left off the
/// Possible Duplicates list. Stored once per pair with the smaller ID in <see cref="RunnerAId"/>
/// (see <see cref="Order"/>); deleted along with either runner.
/// </summary>
public class RunnerNotDuplicate
{
    [Key]
    public Guid Id { get; set; }

    public Guid RunnerAId { get; set; }

    public Guid RunnerBId { get; set; }

    public DateTime DismissedAt { get; set; } = DateTime.UtcNow;

    /// <summary>User ID of the admin/manager who marked the pair; null if unknown.</summary>
    public string? DismissedByUserId { get; set; }

    public virtual Runner RunnerA { get; set; } = null!;
    public virtual Runner RunnerB { get; set; } = null!;

    /// <summary>The pair in stored order (smaller ID first), so (x, y) and (y, x) are one row.</summary>
    public static (Guid A, Guid B) Order(Guid x, Guid y) => x.CompareTo(y) <= 0 ? (x, y) : (y, x);
}
