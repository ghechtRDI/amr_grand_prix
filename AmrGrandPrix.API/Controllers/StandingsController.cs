using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Controller for Grand Prix standings and points
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StandingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IGrandPrixCalculationService _grandPrixCalculationService;
    private readonly ILogger<StandingsController> _logger;

    public StandingsController(
        ApplicationDbContext context,
        IGrandPrixCalculationService grandPrixCalculationService,
        ILogger<StandingsController> logger)
    {
        _context = context;
        _grandPrixCalculationService = grandPrixCalculationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all standings for a specific year
    /// </summary>
    [HttpGet("{year}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<StandingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StandingDto>>> GetStandingsByYear(int year)
    {
        var standings = await _context.GrandPrixStandings
            .Include(s => s.Runner)
            .Where(s => s.Year == year)
            .OrderByDescending(s => s.TotalPoints)
            .ThenByDescending(s => s.BestRacePoints)
            .ThenByDescending(s => s.SecondBestRacePoints)
            .Select(s => new StandingDto
            {
                StandingId = s.StandingId,
                RunnerId = s.RunnerId,
                RunnerName = $"{s.Runner.FirstName} {s.Runner.LastName}",
                Year = s.Year,
                Division = s.Division,
                AgeCategory = s.AgeCategory,
                TotalPoints = s.TotalPoints,
                RacesCompleted = s.RacesCompleted,
                RacesCounted = s.RacesCounted,
                BestRacePoints = s.BestRacePoints,
                SecondBestRacePoints = s.SecondBestRacePoints,
                RunTheGamutQualified = s.RunTheGamutQualified,
                Rank = s.Rank,
                LastUpdated = s.LastUpdated
            })
            .ToListAsync();

        return Ok(standings);
    }

    /// <summary>
    /// Get standings for a specific division (Open Male or Open Female)
    /// </summary>
    [HttpGet("{year}/open/{gender}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StandingsLeaderboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StandingsLeaderboardResponse>> GetOpenDivisionStandings(
        int year,
        string gender)
    {
        // Parse gender
        var genderEnum = ParseGender(gender);

        if (!genderEnum.HasValue)
        {
            return BadRequest("Invalid gender. Use 'male', 'female', or 'nonbinary'");
        }

        var division = GrandPrixConstants.GetOpenDivision(genderEnum.Value);

        var standings = await _context.GrandPrixStandings
            .Include(s => s.Runner)
            .Where(s => s.Year == year && s.Division == division)
            .OrderBy(s => s.Rank)
            .Select(s => new StandingDetailDto
            {
                StandingId = s.StandingId,
                RunnerId = s.RunnerId,
                RunnerName = $"{s.Runner.FirstName} {s.Runner.LastName}",
                Year = s.Year,
                Division = s.Division,
                AgeCategory = s.AgeCategory,
                TotalPoints = s.TotalPoints,
                RacesCompleted = s.RacesCompleted,
                RacesCounted = s.RacesCounted,
                BestRacePoints = s.BestRacePoints,
                SecondBestRacePoints = s.SecondBestRacePoints,
                RunTheGamutQualified = s.RunTheGamutQualified,
                Rank = s.Rank,
                LastUpdated = s.LastUpdated,
                Runner = new RunnerDto
                {
                    RunnerId = s.Runner.RunnerId,
                    FirstName = s.Runner.FirstName,
                    LastName = s.Runner.LastName,
                    Gender = s.Runner.Gender,
                    DateOfBirth = s.Runner.DateOfBirth
                },
                PointsBreakdown = new List<GrandPrixPointsDto>()
            })
            .ToListAsync();

        // Load points breakdown for each runner
        foreach (var standing in standings)
        {
            var points = await _context.GrandPrixPoints
                .Include(p => p.Race)
                .Where(p => p.RunnerId == standing.RunnerId &&
                           p.Year == year &&
                           p.Division == division)
                .Select(p => new GrandPrixPointsDto
                {
                    PointsId = p.PointsId,
                    RaceId = p.RaceId,
                    RaceName = RaceProjections.DisplayName(p.Race.RaceVariant.RaceSeries.Name, p.Race.RaceVariant.Name, p.Race.RaceVariant.RaceSeries.Variants.Count),
                    Points = p.Points,
                    IsRecordBonus = p.IsRecordBonus,
                    Division = p.Division,
                    AgeCategory = p.AgeCategory
                })
                .ToListAsync();

            standing.PointsBreakdown = points;
        }

        var response = new StandingsLeaderboardResponse
        {
            Year = year,
            Division = division,
            AgeCategory = null,
            Standings = standings,
            TotalRunners = standings.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Get standings for a specific age category
    /// </summary>
    [HttpGet("{year}/age/{category}/{gender}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StandingsLeaderboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StandingsLeaderboardResponse>> GetAgeDivisionStandings(
        int year,
        string category,
        string gender)
    {
        // Parse gender
        var genderEnum = ParseGender(gender);

        if (!genderEnum.HasValue)
        {
            return BadRequest("Invalid gender. Use 'male', 'female', or 'nonbinary'");
        }

        var division = GrandPrixConstants.GetAgeDivision(genderEnum.Value);

        var standings = await _context.GrandPrixStandings
            .Include(s => s.Runner)
            .Where(s => s.Year == year &&
                       s.Division == division &&
                       s.AgeCategory == category)
            .OrderBy(s => s.Rank)
            .Select(s => new StandingDetailDto
            {
                StandingId = s.StandingId,
                RunnerId = s.RunnerId,
                RunnerName = $"{s.Runner.FirstName} {s.Runner.LastName}",
                Year = s.Year,
                Division = s.Division,
                AgeCategory = s.AgeCategory,
                TotalPoints = s.TotalPoints,
                RacesCompleted = s.RacesCompleted,
                RacesCounted = s.RacesCounted,
                BestRacePoints = s.BestRacePoints,
                SecondBestRacePoints = s.SecondBestRacePoints,
                RunTheGamutQualified = s.RunTheGamutQualified,
                Rank = s.Rank,
                LastUpdated = s.LastUpdated,
                Runner = new RunnerDto
                {
                    RunnerId = s.Runner.RunnerId,
                    FirstName = s.Runner.FirstName,
                    LastName = s.Runner.LastName,
                    Gender = s.Runner.Gender,
                    DateOfBirth = s.Runner.DateOfBirth
                },
                PointsBreakdown = new List<GrandPrixPointsDto>()
            })
            .ToListAsync();

        // Load points breakdown for each runner
        foreach (var standing in standings)
        {
            var points = await _context.GrandPrixPoints
                .Include(p => p.Race)
                .Where(p => p.RunnerId == standing.RunnerId &&
                           p.Year == year &&
                           p.Division == division &&
                           p.AgeCategory == category)
                .Select(p => new GrandPrixPointsDto
                {
                    PointsId = p.PointsId,
                    RaceId = p.RaceId,
                    RaceName = RaceProjections.DisplayName(p.Race.RaceVariant.RaceSeries.Name, p.Race.RaceVariant.Name, p.Race.RaceVariant.RaceSeries.Variants.Count),
                    Points = p.Points,
                    IsRecordBonus = p.IsRecordBonus,
                    Division = p.Division,
                    AgeCategory = p.AgeCategory
                })
                .ToListAsync();

            standing.PointsBreakdown = points;
        }

        var response = new StandingsLeaderboardResponse
        {
            Year = year,
            Division = division,
            AgeCategory = category,
            Standings = standings,
            TotalRunners = standings.Count
        };

        return Ok(response);
    }

    /// <summary>
    /// Full points breakdown for one gender: every runner's points in every GP race of the year,
    /// for the Overall (Open) division and then each age group.
    /// </summary>
    [HttpGet("{year}/breakdown/{gender}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StandingsBreakdownResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StandingsBreakdownResponse>> GetBreakdown(int year, string gender)
    {
        var genderEnum = ParseGender(gender);
        if (!genderEnum.HasValue)
        {
            return BadRequest("Invalid gender. Use 'male', 'female', or 'nonbinary'");
        }

        var openDivision = GrandPrixConstants.GetOpenDivision(genderEnum.Value);
        var ageDivision = GrandPrixConstants.GetAgeDivision(genderEnum.Value);

        var races = await GetGrandPrixRacesAsync(year);
        var raceById = races.ToDictionary(r => r.RaceId);
        var columns = ToSeriesColumns(races);

        var standings = await _context.GrandPrixStandings
            .Include(s => s.Runner)
            .Where(s => s.Year == year && (s.Division == openDivision || s.Division == ageDivision))
            .ToListAsync();

        var points = await _context.GrandPrixPoints
            .Where(p => p.Year == year && (p.Division == openDivision || p.Division == ageDivision))
            .ToListAsync();
        var pointsByStanding = points.ToLookup(p => (p.Division, p.AgeCategory, p.RunnerId));

        BreakdownRowDto ToRow(GrandPrixStanding s)
        {
            // Same best-4 selection as the standings calculation (ties broken by race date)
            var cells = pointsByStanding[(s.Division, s.AgeCategory, s.RunnerId)]
                .Where(p => raceById.ContainsKey(p.RaceId))
                .OrderByDescending(p => p.Points)
                .ThenBy(p => raceById[p.RaceId].Date)
                .Select((p, i) => new BreakdownCellDto
                {
                    RaceId = p.RaceId,
                    Points = p.Points,
                    IsRecordBonus = p.IsRecordBonus,
                    Counted = i < GrandPrixConstants.BestRacesCounted
                });

            return new BreakdownRowDto
            {
                RunnerId = s.RunnerId,
                RunnerName = $"{s.Runner.FirstName} {s.Runner.LastName}",
                Rank = s.Rank,
                TotalPoints = s.TotalPoints,
                RacesCompleted = s.RacesCompleted,
                RunTheGamutQualified = s.RunTheGamutQualified,
                // One cell per series; a runner normally scores on only one of a series' courses,
                // so if there are two the higher-scoring one is shown
                Points = cells
                    .GroupBy(c => raceById[c.RaceId].RaceSeriesId)
                    .ToDictionary(g => g.Key, g => g.First())
            };
        }

        BreakdownSectionDto ToSection(Division division, string? ageCategory) => new()
        {
            Division = division,
            AgeCategory = ageCategory,
            Rows = standings
                .Where(s => s.Division == division && s.AgeCategory == ageCategory)
                .OrderBy(s => s.Rank)
                .ThenBy(s => s.Runner.LastName)
                .Select(ToRow)
                .ToList()
        };

        var season = await _context.GrandPrixSeasons.FindAsync(year);

        return Ok(new StandingsBreakdownResponse
        {
            Year = year,
            Gender = genderEnum.Value,
            IsFinalized = season?.IsFinalized ?? false,
            Columns = columns,
            Overall = ToSection(openDivision, null),
            AgeGroups = GrandPrixConstants.AgeCategories
                .Select(c => ToSection(ageDivision, c.Name))
                .Where(section => section.Rows.Count > 0)
                .ToList()
        });
    }

    /// <summary>
    /// Runners who completed at least <see cref="GrandPrixConstants.RunTheGamutMinRaces"/> of the
    /// year's Grand Prix events ("Run the Gamut"), whether or not they scored points, with the
    /// events each one finished.
    /// </summary>
    [HttpGet("{year}/gamut")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GamutResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GamutResponse>> GetGamut(int year)
    {
        var columns = ToSeriesColumns(await GetGrandPrixRacesAsync(year));
        var completions = await _grandPrixCalculationService.GetGamutCompletionsAsync(year);

        var qualifiedIds = completions
            .Where(c => c.Value.Count >= GrandPrixConstants.RunTheGamutMinRaces)
            .Select(c => c.Key)
            .ToList();
        var runners = await _context.Runners
            .Where(r => qualifiedIds.Contains(r.RunnerId))
            .ToListAsync();

        var season = await _context.GrandPrixSeasons.FindAsync(year);

        return Ok(new GamutResponse
        {
            Year = year,
            IsFinalized = season?.IsFinalized ?? false,
            MinEvents = GrandPrixConstants.RunTheGamutMinRaces,
            Columns = columns,
            Runners = runners
                .OrderByDescending(r => completions[r.RunnerId].Count)
                .ThenBy(r => r.LastName)
                .ThenBy(r => r.FirstName)
                .Select(r => new GamutRunnerDto
                {
                    RunnerId = r.RunnerId,
                    RunnerName = $"{r.FirstName} {r.LastName}",
                    Gender = r.Gender,
                    EventsCompleted = completions[r.RunnerId].Count,
                    CompletedSeriesIds = columns
                        .Where(c => completions[r.RunnerId].Contains(c.RaceSeriesId))
                        .Select(c => c.RaceSeriesId)
                        .ToList()
                })
                .ToList()
        });
    }

    private sealed record GrandPrixRaceInfo(Guid RaceId, DateOnly Date, Guid RaceSeriesId, string SeriesName);

    private Task<List<GrandPrixRaceInfo>> GetGrandPrixRacesAsync(int year) => _context.Races
        .Where(r => r.Year == year && r.IsGrandPrixRace)
        .Select(r => new GrandPrixRaceInfo(r.RaceId, r.Date, r.RaceVariant.RaceSeriesId, r.RaceVariant.RaceSeries.Name))
        .ToListAsync();

    /// <summary>One column per series with a GP race, in date order.</summary>
    private static List<BreakdownColumnDto> ToSeriesColumns(List<GrandPrixRaceInfo> races) => races
        .GroupBy(r => r.RaceSeriesId)
        .Select(g => new BreakdownColumnDto
        {
            RaceSeriesId = g.Key,
            Name = g.First().SeriesName,
            Date = g.Min(r => r.Date),
            RaceIds = g.OrderBy(r => r.Date).Select(r => r.RaceId).ToList()
        })
        .OrderBy(c => c.Date)
        .ToList();

    private static Gender? ParseGender(string gender) => gender.ToLowerInvariant() switch
    {
        "male" or "m" => Gender.Male,
        "female" or "f" => Gender.Female,
        "nonbinary" or "nb" or "x" => Gender.Nonbinary,
        _ => null
    };

    /// <summary>
    /// Get all Grand Prix history for a specific runner
    /// </summary>
    [HttpGet("runner/{runnerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<StandingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StandingDto>>> GetRunnerHistory(Guid runnerId)
    {
        var runner = await _context.Runners.FindAsync(runnerId);
        if (runner == null)
        {
            return NotFound($"Runner with ID {runnerId} not found");
        }

        var standings = await _context.GrandPrixStandings
            .Include(s => s.Runner)
            .Where(s => s.RunnerId == runnerId)
            .OrderByDescending(s => s.Year)
            .ThenBy(s => s.Division)
            .Select(s => new StandingDto
            {
                StandingId = s.StandingId,
                RunnerId = s.RunnerId,
                RunnerName = $"{s.Runner.FirstName} {s.Runner.LastName}",
                Year = s.Year,
                Division = s.Division,
                AgeCategory = s.AgeCategory,
                TotalPoints = s.TotalPoints,
                RacesCompleted = s.RacesCompleted,
                RacesCounted = s.RacesCounted,
                BestRacePoints = s.BestRacePoints,
                SecondBestRacePoints = s.SecondBestRacePoints,
                RunTheGamutQualified = s.RunTheGamutQualified,
                Rank = s.Rank,
                LastUpdated = s.LastUpdated
            })
            .ToListAsync();

        return Ok(standings);
    }

    /// <summary>
    /// Manually recalculate standings for a specific year. Not allowed once the year is finalized.
    /// </summary>
    [HttpPost("{year}/recalculate")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecalculateStandings(int year)
    {
        if (await _grandPrixCalculationService.IsSeasonFinalizedAsync(year))
            return Conflict(new { message = GrandPrixSeasonFinalizedException.MessageFor(year) });

        try
        {
            _logger.LogInformation("Manually recalculating standings for year {Year}", year);
            var racesProcessed = await RecalculateYearAsync(year);

            return Ok(new
            {
                message = $"Successfully recalculated standings for {year}",
                racesProcessed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating standings for year {Year}", year);
            return BadRequest($"Error recalculating standings: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether a year's Grand Prix is finalized, plus a summary of its GP races for the admin
    /// finalize confirmation.
    /// </summary>
    [HttpGet("{year}/season")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GrandPrixSeasonDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GrandPrixSeasonDto>> GetSeason(int year) => Ok(await BuildSeasonDtoAsync(year));

    /// <summary>
    /// Marks a year's Grand Prix as over: recalculates standings one final time, then locks them so
    /// no result change or recalculation can alter that year's points. Admin/Manager only.
    /// </summary>
    [HttpPost("{year}/finalize")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(GrandPrixSeasonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GrandPrixSeasonDto>> FinalizeSeason(int year)
    {
        if (await _grandPrixCalculationService.IsSeasonFinalizedAsync(year))
            return Conflict(new { message = $"The {year} Grand Prix is already finalized" });

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await RecalculateYearAsync(year);

        var season = await _context.GrandPrixSeasons.FindAsync(year);
        if (season == null)
        {
            season = new GrandPrixSeason { Year = year };
            _context.GrandPrixSeasons.Add(season);
        }
        season.IsFinalized = true;
        season.FinalizedAt = DateTime.UtcNow;
        season.FinalizedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation("Grand Prix {Year} finalized by {UserId}", year, season.FinalizedBy);
        return Ok(await BuildSeasonDtoAsync(year));
    }

    /// <summary>
    /// Re-opens a finalized year so results can be corrected and standings recalculated; finalize
    /// it again afterwards. Admin/Manager only.
    /// </summary>
    [HttpPost("{year}/unfinalize")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(GrandPrixSeasonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GrandPrixSeasonDto>> UnfinalizeSeason(int year)
    {
        var season = await _context.GrandPrixSeasons.FindAsync(year);
        if (season is not { IsFinalized: true })
            return Conflict(new { message = $"The {year} Grand Prix isn't finalized" });

        season.IsFinalized = false;
        season.FinalizedAt = null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Grand Prix {Year} un-finalized by {UserId}", year, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return Ok(await BuildSeasonDtoAsync(year));
    }

    /// <summary>Recalculates points for every GP race in the year, then the year's standings.</summary>
    private async Task<int> RecalculateYearAsync(int year)
    {
        var raceIds = await _context.Races
            .Where(r => r.Year == year && r.IsGrandPrixRace)
            .Select(r => r.RaceId)
            .ToListAsync();

        foreach (var raceId in raceIds)
            await _grandPrixCalculationService.CalculateRacePointsAsync(raceId);

        await _grandPrixCalculationService.UpdateStandingsAsync(year);

        _logger.LogInformation("Recalculated standings for {Year}. Processed {RaceCount} races", year, raceIds.Count);
        return raceIds.Count;
    }

    private async Task<GrandPrixSeasonDto> BuildSeasonDtoAsync(int year)
    {
        var season = await _context.GrandPrixSeasons.FindAsync(year);
        var races = await _context.Races
            .Where(r => r.Year == year && r.IsGrandPrixRace)
            .OrderBy(r => r.Date)
            .Select(r => new
            {
                Name = RaceProjections.DisplayName(r.RaceVariant.RaceSeries.Name, r.RaceVariant.Name, r.RaceVariant.RaceSeries.Variants.Count),
                HasResults = r.Results.Any()
            })
            .ToListAsync();

        return new GrandPrixSeasonDto
        {
            Year = year,
            IsFinalized = season?.IsFinalized ?? false,
            FinalizedAt = season?.IsFinalized == true ? season.FinalizedAt : null,
            GrandPrixRaceCount = races.Count,
            GrandPrixRacesWithoutResults = races.Where(r => !r.HasResults).Select(r => r.Name).ToList()
        };
    }

    /// <summary>
    /// Get available years with GP data
    /// </summary>
    [HttpGet("years")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<int>>> GetAvailableYears()
    {
        var years = await _context.GrandPrixStandings
            .Select(s => s.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();

        return Ok(years);
    }

    /// <summary>
    /// Get all age categories for a specific year
    /// </summary>
    [HttpGet("{year}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetAgeCategories(int year)
    {
        var categories = await _context.GrandPrixStandings
            .Where(s => s.Year == year && s.AgeCategory != null)
            .Select(s => s.AgeCategory!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(categories);
    }
}
