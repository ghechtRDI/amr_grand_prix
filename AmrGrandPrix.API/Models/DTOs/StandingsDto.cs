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

/// <summary>
/// Every runner's points in every GP race of a year for one gender — the Overall (Open) division
/// followed by each age group, mirroring the published Grand Prix results sheet.
/// </summary>
public class StandingsBreakdownResponse
{
    public int Year { get; set; }
    public Gender Gender { get; set; }
    public bool IsFinalized { get; set; }

    /// <summary>
    /// One column per race series with a GP race this year, in date order. A series' junior course
    /// (e.g. Jack's Run at Bird Ridge) shares the series' column.
    /// </summary>
    public List<BreakdownColumnDto> Columns { get; set; } = new();

    public BreakdownSectionDto Overall { get; set; } = null!;

    /// <summary>Age groups in category order; groups with no scorers are omitted.</summary>
    public List<BreakdownSectionDto> AgeGroups { get; set; } = new();
}

public class BreakdownColumnDto
{
    public Guid RaceSeriesId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Date of the series' earliest GP race this year.</summary>
    public DateOnly Date { get; set; }

    public List<Guid> RaceIds { get; set; } = new();
}

public class BreakdownSectionDto
{
    public Division Division { get; set; }
    public string? AgeCategory { get; set; }
    public List<BreakdownRowDto> Rows { get; set; } = new();
}

public class BreakdownRowDto
{
    public Guid RunnerId { get; set; }
    public string RunnerName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int TotalPoints { get; set; }
    public int RacesCompleted { get; set; }
    public bool RunTheGamutQualified { get; set; }

    /// <summary>Points per column, keyed by RaceSeriesId; series the runner didn't score in are absent.</summary>
    public Dictionary<Guid, BreakdownCellDto> Points { get; set; } = new();
}

public class BreakdownCellDto
{
    public Guid RaceId { get; set; }
    public int Points { get; set; }
    public bool IsRecordBonus { get; set; }

    /// <summary>Whether this race is one of the runner's best 4 that make up the total.</summary>
    public bool Counted { get; set; }
}

/// <summary>
/// Runners who "Ran the Gamut" in a year — completed at least <see cref="MinEvents"/> of its
/// Grand Prix events.
/// </summary>
public class GamutResponse
{
    public int Year { get; set; }
    public bool IsFinalized { get; set; }
    public int MinEvents { get; set; }

    /// <summary>The year's GP events (one per series), in date order.</summary>
    public List<BreakdownColumnDto> Columns { get; set; } = new();

    /// <summary>Most events first, then by last name.</summary>
    public List<GamutRunnerDto> Runners { get; set; } = new();
}

public class GamutRunnerDto
{
    public Guid RunnerId { get; set; }
    public string RunnerName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public int EventsCompleted { get; set; }

    /// <summary>RaceSeriesIds of the events finished, matching <see cref="GamutResponse.Columns"/>.</summary>
    public List<Guid> CompletedSeriesIds { get; set; } = new();
}
