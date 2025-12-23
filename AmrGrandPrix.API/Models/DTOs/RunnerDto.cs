namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// DTO for Runner data transfer
/// </summary>
public class RunnerDto
{
    public Guid RunnerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Email { get; set; }
    public int TotalRaces { get; set; }
}

