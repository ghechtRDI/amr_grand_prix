namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// DTO for RaceResult data transfer
/// </summary>
public class RaceResultDto
{
    public Guid ResultId { get; set; }
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public DateTime RaceDate { get; set; }
    public Guid RunnerId { get; set; }
    public string RunnerName { get; set; } = string.Empty;
    public int? Bib { get; set; }
    public int? Place { get; set; }
    public int? PlaceGender { get; set; }
    public int? PlaceAgeCategory { get; set; }
    public TimeSpan? Time { get; set; }
    public int Age { get; set; }
    public Gender Gender { get; set; }
    public ResultStatus Status { get; set; }
    public string? Notes { get; set; }
    public bool IsNewRecord { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for detailed race result with runner information
/// </summary>
public class RaceResultDetailDto : RaceResultDto
{
    public RunnerDto Runner { get; set; } = null!;
    public RaceDto Race { get; set; } = null!;
    public List<GrandPrixPointsDto>? GrandPrixPoints { get; set; }
}
