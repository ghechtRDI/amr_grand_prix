namespace AmrGrandPrix.API.Models.DTOs;

/// <summary>
/// DTO for Grand Prix Points
/// </summary>
public class GrandPrixPointsDto
{
    public Guid PointsId { get; set; }
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsRecordBonus { get; set; }
    public Division Division { get; set; }
    public string? AgeCategory { get; set; }
}

/// <summary>
/// DTO for Grand Prix Standing
/// </summary>
public class StandingDto
{
    public Guid StandingId { get; set; }
    public Guid RunnerId { get; set; }
    public string RunnerName { get; set; } = string.Empty;
    public int Year { get; set; }
    public Division Division { get; set; }
    public string? AgeCategory { get; set; }
    public int TotalPoints { get; set; }
    public int RacesCompleted { get; set; }
    public int RacesCounted { get; set; }
    public int BestRacePoints { get; set; }
    public int SecondBestRacePoints { get; set; }
    public bool RunTheGamutQualified { get; set; }
    public int Rank { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Detailed standing with runner and race details
/// </summary>
public class StandingDetailDto : StandingDto
{
    public RunnerDto Runner { get; set; } = null!;
    public List<GrandPrixPointsDto> PointsBreakdown { get; set; } = new();
}

/// <summary>
/// Response for standings leaderboard
/// </summary>
public class StandingsLeaderboardResponse
{
    public int Year { get; set; }
    public Division Division { get; set; }
    public string? AgeCategory { get; set; }
    public List<StandingDetailDto> Standings { get; set; } = new();
    public int TotalRunners { get; set; }
}

/// <summary>
/// Finalization status of one year's Grand Prix
/// </summary>
public class GrandPrixSeasonDto
{
    public int Year { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public int GrandPrixRaceCount { get; set; }

    /// <summary>GP races this year with no results yet — worth a second look before finalizing.</summary>
    public List<string> GrandPrixRacesWithoutResults { get; set; } = new();
}
