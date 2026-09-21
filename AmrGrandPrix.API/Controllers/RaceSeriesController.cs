using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.RaceStatistics;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Groups of races that are the same physical event across years/variants (e.g. every running of
/// Mount Marathon), so they can be browsed and compared together.
/// </summary>
[ApiController]
[Route("api/race-series")]
public class RaceSeriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRaceStatisticsService _statisticsService;
    private readonly ILogger<RaceSeriesController> _logger;

    public RaceSeriesController(
        ApplicationDbContext context,
        IRaceStatisticsService statisticsService,
        ILogger<RaceSeriesController> logger)
    {
        _context = context;
        _statisticsService = statisticsService;
        _logger = logger;
    }

    /// <summary>
    /// Top-20-all-time, course record history, and age-group records for one course
    /// variant/gender within this series.
    /// </summary>
    [HttpGet("{id}/statistics")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RaceSeriesStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceSeriesStatisticsDto>> GetStatistics(
        Guid id, [FromQuery] string? variant, [FromQuery] Gender gender)
    {
        var seriesExists = await _context.RaceSeries.AnyAsync(s => s.RaceSeriesId == id);
        if (!seriesExists)
            return NotFound($"Race series {id} not found");

        var stats = await _statisticsService.GetStatisticsAsync(id, variant, gender);
        return Ok(stats);
    }

    /// <summary>
    /// List all race series.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceSeriesSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RaceSeriesSummaryDto>>> GetAll()
    {
        var series = await _context.RaceSeries
            .OrderBy(s => s.Name)
            .Select(s => new RaceSeriesSummaryDto
            {
                RaceSeriesId = s.RaceSeriesId,
                Name = s.Name,
                Description = s.Description,
                RaceCount = s.Races.Count,
                IsGrandPrixSeries = s.Races.Any(r => r.IsGrandPrixRace),
                MostRecentYear = s.Races.Any() ? s.Races.Max(r => r.Year) : (int?)null
            })
            .ToListAsync();

        return Ok(series);
    }

    /// <summary>
    /// Get a series with every race instance, ordered by year.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RaceSeriesDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceSeriesDetailDto>> GetById(Guid id)
    {
        var series = await _context.RaceSeries
            .Include(s => s.Races)
            .ThenInclude(r => r.Results)
            .FirstOrDefaultAsync(s => s.RaceSeriesId == id);

        if (series == null)
            return NotFound($"Race series {id} not found");

        var dto = new RaceSeriesDetailDto
        {
            RaceSeriesId = series.RaceSeriesId,
            Name = series.Name,
            Description = series.Description,
            IsGrandPrixSeries = series.Races.Any(r => r.IsGrandPrixRace),
            Races = series.Races
                .OrderByDescending(r => r.Date)
                .Select(r => new RaceSeriesInstanceDto
                {
                    RaceId = r.RaceId,
                    Year = r.Year,
                    Date = r.Date,
                    CourseVariant = r.CourseVariant,
                    IsGrandPrixRace = r.IsGrandPrixRace,
                    ResultsCount = r.Results.Count
                })
                .ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Create a new series. Admin/Manager only.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceSeriesSummaryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<RaceSeriesSummaryDto>> Create([FromBody] CreateRaceSeriesRequest request)
    {
        var series = new RaceSeries
        {
            RaceSeriesId = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.RaceSeries.Add(series);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created race series {SeriesName} ({SeriesId})", series.Name, series.RaceSeriesId);

        return CreatedAtAction(nameof(GetById), new { id = series.RaceSeriesId }, new RaceSeriesSummaryDto
        {
            RaceSeriesId = series.RaceSeriesId,
            Name = series.Name,
            Description = series.Description,
            RaceCount = 0,
            IsGrandPrixSeries = false,
            MostRecentYear = null
        });
    }

    /// <summary>
    /// Rename/update a series. Admin/Manager only.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateRaceSeriesRequest request)
    {
        var series = await _context.RaceSeries.FindAsync(id);
        if (series == null)
            return NotFound($"Race series {id} not found");

        series.Name = request.Name;
        series.Description = request.Description;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Series updated" });
    }

    /// <summary>
    /// Merges one or more source series into a target series: every race currently in a source
    /// series is reassigned to the target, and the (now-empty) source series are deleted. Use
    /// this to fix a case the name-based backfill got wrong — e.g. "Blueberry Rampage" and
    /// "Blueberry Rampage - Junior Blueberry Knoll" ending up as separate series because the
    /// variant was typed into the race Name instead of relying solely on CourseVariant.
    /// Admin/Manager only.
    /// </summary>
    [HttpPost("{targetId}/merge")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Merge(Guid targetId, [FromBody] MergeRaceSeriesRequest request)
    {
        var target = await _context.RaceSeries.FindAsync(targetId);
        if (target == null)
            return NotFound($"Race series {targetId} not found");

        var sourceIds = request.SourceSeriesIds.Where(id => id != targetId).Distinct().ToList();
        if (sourceIds.Count == 0)
            return BadRequest(new { message = "No source series to merge" });

        var sourceSeries = await _context.RaceSeries
            .Where(s => sourceIds.Contains(s.RaceSeriesId))
            .ToListAsync();

        var racesMoved = await _context.Races
            .Where(r => r.RaceSeriesId != null && sourceIds.Contains(r.RaceSeriesId.Value))
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RaceSeriesId, targetId));

        _context.RaceSeries.RemoveRange(sourceSeries);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Merged {SourceCount} series into {TargetId}, moving {RaceCount} races",
            sourceSeries.Count, targetId, racesMoved);

        return Ok(new { seriesRemoved = sourceSeries.Count, racesMoved });
    }

    /// <summary>
    /// One-time bootstrap: groups every race with no series yet by exact name match, creating a
    /// series per distinct name (or reusing one that already has that name) and linking the races
    /// to it. Safe to re-run — only touches races that still have no series. Admin only.
    /// </summary>
    [HttpPost("backfill")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Backfill()
    {
        var ungroupedRaces = await _context.Races
            .Where(r => r.RaceSeriesId == null)
            .ToListAsync();

        var existingSeriesByName = await _context.RaceSeries.ToDictionaryAsync(s => s.Name, s => s);

        var createdCount = 0;
        var linkedCount = 0;

        foreach (var group in ungroupedRaces.GroupBy(r => r.Name))
        {
            if (!existingSeriesByName.TryGetValue(group.Key, out var series))
            {
                series = new RaceSeries
                {
                    RaceSeriesId = Guid.NewGuid(),
                    Name = group.Key,
                    CreatedAt = DateTime.UtcNow
                };
                _context.RaceSeries.Add(series);
                existingSeriesByName[group.Key] = series;
                createdCount++;
            }

            foreach (var race in group)
            {
                race.RaceSeriesId = series.RaceSeriesId;
                linkedCount++;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Race series backfill created {CreatedCount} series and linked {LinkedCount} races",
            createdCount, linkedCount);

        return Ok(new { seriesCreated = createdCount, racesLinked = linkedCount });
    }
}

public class RaceSeriesSummaryDto
{
    public Guid RaceSeriesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RaceCount { get; set; }
    public bool IsGrandPrixSeries { get; set; }
    public int? MostRecentYear { get; set; }
}

public class RaceSeriesDetailDto
{
    public Guid RaceSeriesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsGrandPrixSeries { get; set; }
    public List<RaceSeriesInstanceDto> Races { get; set; } = new();
}

public class RaceSeriesInstanceDto
{
    public Guid RaceId { get; set; }
    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public string? CourseVariant { get; set; }
    public bool IsGrandPrixRace { get; set; }
    public int ResultsCount { get; set; }
}

public class CreateRaceSeriesRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class MergeRaceSeriesRequest
{
    public List<Guid> SourceSeriesIds { get; set; } = new();
}
