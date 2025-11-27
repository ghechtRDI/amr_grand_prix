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

/// <summary>
/// DTO for runner matching
/// </summary>
public class RunnerMatchDto
{
    public Guid RunnerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public double MatchConfidence { get; set; }
    public string MatchReason { get; set; } = string.Empty;
}

/// <summary>
/// Request for creating a new runner
/// </summary>
public class CreateRunnerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Request for updating an existing runner
/// </summary>
public class UpdateRunnerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Email { get; set; }
}
