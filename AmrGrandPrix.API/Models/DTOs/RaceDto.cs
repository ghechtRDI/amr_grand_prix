namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// DTO for Race data transfer
/// </summary>
public class RaceDto
{
    public Guid RaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsGrandPrixRace { get; set; }
    public int? GrandPrixRaceOrder { get; set; }
    public DateTime Date { get; set; }
    public int Year { get; set; }
    public string? CourseVariant { get; set; }
    public string? Location { get; set; }
    public TimeSpan? RecordTimeMale { get; set; }
    public TimeSpan? RecordTimeFemale { get; set; }
    public string? RecordHolderMale { get; set; }
    public string? RecordHolderFemale { get; set; }
    public int ResultsCount { get; set; }
}

/// <summary>
/// Request for creating a new race
/// </summary>
public class CreateRaceRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsGrandPrixRace { get; set; }
    public int? GrandPrixRaceOrder { get; set; }
    public DateTime Date { get; set; }
    public string? CourseVariant { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request for updating an existing race
/// </summary>
public class UpdateRaceRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? CourseVariant { get; set; }
    public string? Location { get; set; }
    public TimeSpan? RecordTimeMale { get; set; }
    public TimeSpan? RecordTimeFemale { get; set; }
    public string? RecordHolderMale { get; set; }
    public string? RecordHolderFemale { get; set; }
}
