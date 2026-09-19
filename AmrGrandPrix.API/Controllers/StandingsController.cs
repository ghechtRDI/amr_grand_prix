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
[AllowAnonymous]
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
    [ProducesResponseType(typeof(StandingsLeaderboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StandingsLeaderboardResponse>> GetOpenDivisionStandings(
        int year,
        string gender)
    {
        // Parse gender
        var genderEnum = gender.ToLower() switch
        {
            "male" or "m" => (Gender?)Gender.Male,
            "female" or "f" => (Gender?)Gender.Female,
            _ => null
        };

        if (!genderEnum.HasValue)
        {
            return BadRequest("Invalid gender. Use 'male' or 'female'");
        }

        var division = genderEnum.Value == Gender.Male ? Division.OpenMale : Division.OpenFemale;

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
                    DateOfBirth = s.Runner.DateOfBirth,
                    Email = s.Runner.Email
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
                    RaceName = p.Race.Name,
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
    [ProducesResponseType(typeof(StandingsLeaderboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StandingsLeaderboardResponse>> GetAgeDivisionStandings(
        int year,
        string category,
        string gender)
    {
        // Parse gender
        var genderEnum = gender.ToLower() switch
        {
            "male" or "m" => (Gender?)Gender.Male,
            "female" or "f" => (Gender?)Gender.Female,
            _ => null
        };

        if (!genderEnum.HasValue)
        {
            return BadRequest("Invalid gender. Use 'male' or 'female'");
        }

        var division = genderEnum == Gender.Male ? Division.AgeMale : Division.AgeFemale;

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
                    DateOfBirth = s.Runner.DateOfBirth,
                    Email = s.Runner.Email
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
                    RaceName = p.Race.Name,
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
    /// Get all Grand Prix history for a specific runner
    /// </summary>
    [HttpGet("runner/{runnerId}")]
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
    /// Manually recalculate standings for a specific year
    /// </summary>
    [HttpPost("{year}/recalculate")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecalculateStandings(int year)
    {
        try
        {
            _logger.LogInformation("Manually recalculating standings for year {Year}", year);

            // Delete existing standings for this year
            var existingStandings = await _context.GrandPrixStandings
                .Where(s => s.Year == year)
                .ToListAsync();

            _context.GrandPrixStandings.RemoveRange(existingStandings);
            await _context.SaveChangesAsync();

            // Recalculate all GP races for this year
            var races = await _context.Races
                .Where(r => r.Year == year && r.IsGrandPrixRace)
                .ToListAsync();

            foreach (var race in races)
            {
                await _grandPrixCalculationService.CalculateRacePointsAsync(race.RaceId);
            }

            // Update standings
            await _grandPrixCalculationService.UpdateStandingsAsync(year);

            _logger.LogInformation(
                "Successfully recalculated standings for {Year}. Processed {RaceCount} races",
                year, races.Count);

            return Ok(new
            {
                message = $"Successfully recalculated standings for {year}",
                racesProcessed = races.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating standings for year {Year}", year);
            return BadRequest($"Error recalculating standings: {ex.Message}");
        }
    }

    /// <summary>
    /// Get available years with GP data
    /// </summary>
    [HttpGet("years")]
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
