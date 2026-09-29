using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Services.GrandPrix;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Controller for managing races
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RacesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IGrandPrixCalculationService _grandPrixCalculationService;
    private readonly ILogger<RacesController> _logger;

    public RacesController(
        ApplicationDbContext context,
        IGrandPrixCalculationService grandPrixCalculationService,
        ILogger<RacesController> logger)
    {
        _context = context;
        _grandPrixCalculationService = grandPrixCalculationService;
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
            .OrderByDescending(r => r.Date)
            .Select(RaceProjections.ToDto)
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
            .Where(r => r.Year == year)
            .OrderBy(r => r.Date)
            .Select(RaceProjections.ToDto)
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
            .Where(r => r.Year == year && r.IsGrandPrixRace)
            .OrderBy(r => r.Date)
            .Select(RaceProjections.ToDto)
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
            .Where(r => r.RaceId == id)
            .Select(RaceProjections.ToDto)
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound($"Race with ID {id} not found");
        }

        return Ok(race);
    }

    /// <summary>
    /// Create one year's running of a race variant
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceDto>> CreateRace([FromBody] CreateRaceRequest request)
    {
        var variant = await _context.RaceVariants.FindAsync(request.RaceVariantId);
        if (variant == null)
            return BadRequest(new { message = $"Race variant {request.RaceVariantId} not found" });

        var validationError = await ValidateUniqueYearAsync(request.RaceVariantId, request.Date.Year, excludeRaceId: null);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var race = new Race
        {
            RaceId = Guid.NewGuid(),
            RaceVariantId = variant.RaceVariantId,
            IsGrandPrixRace = request.IsGrandPrixRace ?? variant.IsGrandPrixByDefault,
            Date = request.Date,
            Year = request.Date.Year,
            Location = request.Location,
            CreatedAt = DateTime.UtcNow
        };

        _context.Races.Add(race);
        await _context.SaveChangesAsync();

        var raceDto = await _context.Races.Where(r => r.RaceId == race.RaceId).Select(RaceProjections.ToDto).FirstAsync();

        _logger.LogInformation("Created race {RaceName} ({Variant}) {Year} ({RaceId})",
            raceDto.RaceSeriesName, variant.Name, race.Year, race.RaceId);

        return CreatedAtAction(nameof(GetRace), new { id = race.RaceId }, raceDto);
    }

    /// <summary>
    /// Update an existing race. Changing <see cref="UpdateRaceRequest.IsGrandPrixRace"/> (e.g. a
    /// snowy year where a different variant is the Grand Prix race) recalculates points and standings.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceDto>> UpdateRace(Guid id, [FromBody] UpdateRaceRequest request)
    {
        var race = await _context.Races.FindAsync(id);
        if (race == null)
            return NotFound($"Race with ID {id} not found");

        if (!await _context.RaceVariants.AnyAsync(v => v.RaceVariantId == request.RaceVariantId))
            return BadRequest(new { message = $"Race variant {request.RaceVariantId} not found" });

        var validationError = await ValidateUniqueYearAsync(request.RaceVariantId, request.Date.Year, excludeRaceId: id);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var previousYear = race.Year;
        var gpChanged = race.IsGrandPrixRace != request.IsGrandPrixRace || previousYear != request.Date.Year;
        var standingsAffected = gpChanged && (race.IsGrandPrixRace || request.IsGrandPrixRace);

        // Moving a GP race's points in or out of a finalized season would change locked standings
        if (standingsAffected)
        {
            foreach (var year in new[] { previousYear, request.Date.Year }.Distinct())
            {
                if (await _grandPrixCalculationService.IsSeasonFinalizedAsync(year))
                    return Conflict(new { message = GrandPrixSeasonFinalizedException.MessageFor(year) });
            }
        }

        race.RaceVariantId = request.RaceVariantId;
        race.Date = request.Date;
        race.Year = request.Date.Year;
        race.IsGrandPrixRace = request.IsGrandPrixRace;
        race.Location = request.Location;

        await _context.SaveChangesAsync();

        if (standingsAffected)
        {
            await _grandPrixCalculationService.RecalculateAfterResultsChangeAsync(race.RaceId);
            if (previousYear != race.Year)
                await _grandPrixCalculationService.UpdateStandingsAsync(previousYear);
        }

        var raceDto = await _context.Races.Where(r => r.RaceId == id).Select(RaceProjections.ToDto).FirstAsync();

        _logger.LogInformation("Updated race {RaceName} {Year} ({RaceId})", raceDto.RaceSeriesName, race.Year, race.RaceId);

        return Ok(raceDto);
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

            _logger.LogInformation("Deleted race {RaceId} ({Year})", race.RaceId, race.Year);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting race {RaceId}", id);
            return BadRequest($"Error deleting race: {ex.Message}");
        }
    }

    private async Task<string?> ValidateUniqueYearAsync(Guid raceVariantId, int year, Guid? excludeRaceId)
    {
        var exists = await _context.Races.AnyAsync(r =>
            r.RaceVariantId == raceVariantId && r.Year == year && r.RaceId != excludeRaceId);
        return exists ? $"This variant already has a race in {year}" : null;
    }
}
