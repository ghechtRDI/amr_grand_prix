using Microsoft.AspNetCore.Identity;

namespace AmrGrandPrix.API.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
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
