using Microsoft.AspNetCore.Identity;

namespace AmrGrandPrix.API.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? Hometown { get; set; }

    /// <summary>
    /// Other full names this person has raced under (maiden name, married name, a name with a
    /// different spelling, etc.) — up to 3, enforced at the request-validation layer.
    /// </summary>
    public List<string> AlternateNames { get; set; } = new();

    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The Runner this account is linked to, once a claim on it has been approved.
    /// </summary>
    public Guid? RunnerId { get; set; }
    public virtual Runner? Runner { get; set; }
}
