using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Controller for managing races
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RacesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RacesController> _logger;

    public RacesController(
        ApplicationDbContext context,
        ILogger<RacesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all races
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RaceDto>>> GetRaces()
    {
        var races = await _context.Races
            .Include(r => r.Results)
            .OrderByDescending(r => r.Date)
            .Select(r => new RaceDto
            {
                RaceId = r.RaceId,
                Name = r.Name,
                IsGrandPrixRace = r.IsGrandPrixRace,
                GrandPrixRaceOrder = r.GrandPrixRaceOrder,
                Date = r.Date,
                Year = r.Year,
                CourseVariant = r.CourseVariant,
                Location = r.Location,
                RecordTimeMale = r.RecordTimeMale,
                RecordTimeFemale = r.RecordTimeFemale,
                RecordHolderMale = r.RecordHolderMale,
                RecordHolderFemale = r.RecordHolderFemale,
                ResultsCount = r.Results.Count
            })
            .ToListAsync();

        return Ok(races);
    }

    /// <summary>
    /// Get races for a specific year
    /// </summary>
    [HttpGet("{year}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RaceDto>>> GetRacesByYear(int year)
    {
        var races = await _context.Races
            .Include(r => r.Results)
            .Where(r => r.Year == year)
            .OrderBy(r => r.Date)
            .Select(r => new RaceDto
            {
                RaceId = r.RaceId,
                Name = r.Name,
                IsGrandPrixRace = r.IsGrandPrixRace,
                GrandPrixRaceOrder = r.GrandPrixRaceOrder,
                Date = r.Date,
                Year = r.Year,
                CourseVariant = r.CourseVariant,
                Location = r.Location,
                RecordTimeMale = r.RecordTimeMale,
                RecordTimeFemale = r.RecordTimeFemale,
                RecordHolderMale = r.RecordHolderMale,
                RecordHolderFemale = r.RecordHolderFemale,
                ResultsCount = r.Results.Count
            })
            .ToListAsync();

        return Ok(races);
    }

    /// <summary>
    /// Get Grand Prix races for a specific year
    /// </summary>
    [HttpGet("grandprix/{year}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RaceDto>>> GetGrandPrixRacesByYear(int year)
    {
        var races = await _context.Races
            .Include(r => r.Results)
            .Where(r => r.Year == year && r.IsGrandPrixRace)
            .OrderBy(r => r.GrandPrixRaceOrder)
            .Select(r => new RaceDto
            {
                RaceId = r.RaceId,
                Name = r.Name,
                IsGrandPrixRace = r.IsGrandPrixRace,
                GrandPrixRaceOrder = r.GrandPrixRaceOrder,
                Date = r.Date,
                Year = r.Year,
                CourseVariant = r.CourseVariant,
                Location = r.Location,
                RecordTimeMale = r.RecordTimeMale,
                RecordTimeFemale = r.RecordTimeFemale,
                RecordHolderMale = r.RecordHolderMale,
                RecordHolderFemale = r.RecordHolderFemale,
                ResultsCount = r.Results.Count
            })
            .ToListAsync();

        return Ok(races);
    }

    /// <summary>
    /// Get a specific race by ID
    /// </summary>
    [HttpGet("detail/{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceDto>> GetRace(Guid id)
    {
        var race = await _context.Races
            .Include(r => r.Results)
            .Where(r => r.RaceId == id)
            .Select(r => new RaceDto
            {
                RaceId = r.RaceId,
                Name = r.Name,
                IsGrandPrixRace = r.IsGrandPrixRace,
                GrandPrixRaceOrder = r.GrandPrixRaceOrder,
                Date = r.Date,
                Year = r.Year,
                CourseVariant = r.CourseVariant,
                Location = r.Location,
                RecordTimeMale = r.RecordTimeMale,
                RecordTimeFemale = r.RecordTimeFemale,
                RecordHolderMale = r.RecordHolderMale,
                RecordHolderFemale = r.RecordHolderFemale,
                ResultsCount = r.Results.Count
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound($"Race with ID {id} not found");
        }

        return Ok(race);
    }

    /// <summary>
    /// Create a new race
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceDto>> CreateRace([FromBody] CreateRaceRequest request)
    {
        try
        {
            // Validate Grand Prix race order if applicable
            if (request.IsGrandPrixRace)
            {
                if (!request.GrandPrixRaceOrder.HasValue ||
                    request.GrandPrixRaceOrder < 1 ||
                    request.GrandPrixRaceOrder > 9)
                {
                    return BadRequest("Grand Prix races must have an order between 1 and 9");
                }

                // Check for duplicate GP order in same year
                var year = request.Date.Year;
                var existingGpRace = await _context.Races
                    .AnyAsync(r => r.Year == year &&
                                  r.IsGrandPrixRace &&
                                  r.GrandPrixRaceOrder == request.GrandPrixRaceOrder);

                if (existingGpRace)
                {
                    return BadRequest($"A Grand Prix race with order {request.GrandPrixRaceOrder} already exists for year {year}");
                }
            }

            var race = new Race
            {
                RaceId = Guid.NewGuid(),
                Name = request.Name,
                IsGrandPrixRace = request.IsGrandPrixRace,
                GrandPrixRaceOrder = request.GrandPrixRaceOrder,
                Date = request.Date,
                Year = request.Date.Year,
                CourseVariant = request.CourseVariant,
                Location = request.Location,
                CreatedAt = DateTime.UtcNow
            };

            _context.Races.Add(race);
            await _context.SaveChangesAsync();

            var raceDto = new RaceDto
            {
                RaceId = race.RaceId,
                Name = race.Name,
                IsGrandPrixRace = race.IsGrandPrixRace,
                GrandPrixRaceOrder = race.GrandPrixRaceOrder,
                Date = race.Date,
                Year = race.Year,
                CourseVariant = race.CourseVariant,
                Location = race.Location,
                RecordTimeMale = race.RecordTimeMale,
                RecordTimeFemale = race.RecordTimeFemale,
                RecordHolderMale = race.RecordHolderMale,
                RecordHolderFemale = race.RecordHolderFemale,
                ResultsCount = 0
            };

            _logger.LogInformation("Created new race: {RaceName} ({RaceId})", race.Name, race.RaceId);

            return CreatedAtAction(nameof(GetRace), new { id = race.RaceId }, raceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating race");
            return BadRequest($"Error creating race: {ex.Message}");
        }
    }

    /// <summary>
    /// Update an existing race
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceDto>> UpdateRace(Guid id, [FromBody] UpdateRaceRequest request)
    {
        try
        {
            var race = await _context.Races.FindAsync(id);
            if (race == null)
            {
                return NotFound($"Race with ID {id} not found");
            }

            // Update allowed fields
            race.Name = request.Name;
            race.Date = request.Date;
            race.Year = request.Date.Year;
            race.CourseVariant = request.CourseVariant;
            race.Location = request.Location;
            race.RecordTimeMale = request.RecordTimeMale;
            race.RecordTimeFemale = request.RecordTimeFemale;
            race.RecordHolderMale = request.RecordHolderMale;
            race.RecordHolderFemale = request.RecordHolderFemale;

            await _context.SaveChangesAsync();

            var raceDto = new RaceDto
            {
                RaceId = race.RaceId,
                Name = race.Name,
                IsGrandPrixRace = race.IsGrandPrixRace,
                GrandPrixRaceOrder = race.GrandPrixRaceOrder,
                Date = race.Date,
                Year = race.Year,
                CourseVariant = race.CourseVariant,
                Location = race.Location,
                RecordTimeMale = race.RecordTimeMale,
                RecordTimeFemale = race.RecordTimeFemale,
                RecordHolderMale = race.RecordHolderMale,
                RecordHolderFemale = race.RecordHolderFemale,
                ResultsCount = race.Results?.Count ?? 0
            };

            _logger.LogInformation("Updated race: {RaceName} ({RaceId})", race.Name, race.RaceId);

            return Ok(raceDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating race {RaceId}", id);
            return BadRequest($"Error updating race: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a race (only if no results exist)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteRace(Guid id)
    {
        try
        {
            var race = await _context.Races
                .Include(r => r.Results)
                .FirstOrDefaultAsync(r => r.RaceId == id);

            if (race == null)
            {
                return NotFound($"Race with ID {id} not found");
            }

            // Prevent deletion if results exist
            if (race.Results.Any())
            {
                return BadRequest($"Cannot delete race with existing results. Delete results first.");
            }

            _context.Races.Remove(race);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted race: {RaceName} ({RaceId})", race.Name, race.RaceId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting race {RaceId}", id);
            return BadRequest($"Error deleting race: {ex.Message}");
        }
    }
}
