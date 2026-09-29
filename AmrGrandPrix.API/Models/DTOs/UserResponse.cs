namespace AmrGrandPrix.API.Models.DTOs;

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredName { get; set; }
    public string? Hometown { get; set; }
    public List<string> AlternateNames { get; set; } = new();
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public Guid? RunnerId { get; set; }
}
