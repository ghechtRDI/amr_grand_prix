using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Services.RaceStatistics;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Recurring events and their course variants. Every race belongs to exactly one variant of one
/// series, so results for the same course line up year over year.
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
        Guid id, [FromQuery] Guid variantId, [FromQuery] Gender gender)
    {
        var variantInSeries = await _context.RaceVariants
            .AnyAsync(v => v.RaceVariantId == variantId && v.RaceSeriesId == id);
        if (!variantInSeries)
            return NotFound($"Variant {variantId} not found in race series {id}");

        var stats = await _statisticsService.GetStatisticsAsync(variantId, gender);
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
                VariantCount = s.Variants.Count,
                RaceCount = s.Variants.SelectMany(v => v.Races).Count(),
                IsGrandPrixSeries = s.Variants.Any(v => v.IsGrandPrixByDefault || v.Races.Any(r => r.IsGrandPrixRace)),
                MostRecentYear = s.Variants.SelectMany(v => v.Races).Max(r => (int?)r.Year)
            })
            .ToListAsync();

        return Ok(series);
    }

    /// <summary>
    /// Get a series with its variants and every race instance, newest first.
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RaceSeriesDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RaceSeriesDetailDto>> GetById(Guid id)
    {
        var series = await _context.RaceSeries
            .Include(s => s.Variants)
            .ThenInclude(v => v.Races)
            .ThenInclude(r => r.Results)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.RaceSeriesId == id);

        if (series == null)
            return NotFound($"Race series {id} not found");

        var hasMultipleVariants = series.Variants.Count > 1;
        var dto = new RaceSeriesDetailDto
        {
            RaceSeriesId = series.RaceSeriesId,
            Name = series.Name,
            Description = series.Description,
            IsGrandPrixSeries = series.Variants.Any(v => v.IsGrandPrixByDefault || v.Races.Any(r => r.IsGrandPrixRace)),
            Variants = series.Variants
                .OrderBy(v => v.DisplayOrder).ThenBy(v => v.Name)
                .Select(ToVariantDto)
                .ToList(),
            Races = series.Variants
                .SelectMany(v => v.Races.Select(r => new RaceSeriesInstanceDto
                {
                    RaceId = r.RaceId,
                    Year = r.Year,
                    Date = r.Date,
                    RaceVariantId = v.RaceVariantId,
                    CourseVariant = hasMultipleVariants ? v.Name : null,
                    VariantDisplayOrder = v.DisplayOrder,
                    IsGrandPrixRace = r.IsGrandPrixRace,
                    ResultsCount = r.Results.Count
                }))
                .OrderByDescending(r => r.Date)
                .ThenBy(r => r.VariantDisplayOrder)
                .ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Create a new series with its variants. With no variants given, a single
    /// "Standard" variant is created. Admin/Manager only.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceSeriesSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceSeriesSummaryDto>> Create([FromBody] CreateRaceSeriesRequest request)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "Series name is required" });
        if (await _context.RaceSeries.AnyAsync(s => s.Name == name))
            return BadRequest(new { message = $"A series named \"{name}\" already exists" });

        var variantRequests = request.Variants.Count > 0
            ? request.Variants
            : [new RaceVariantRequest { Name = RaceVariant.StandardName, IsGrandPrixByDefault = request.IsGrandPrix }];

        var duplicateVariant = variantRequests
            .GroupBy(v => v.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateVariant != null)
            return BadRequest(new { message = $"Duplicate variant name \"{duplicateVariant.Key}\"" });

        var series = new RaceSeries
        {
            RaceSeriesId = Guid.NewGuid(),
            Name = name,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };
        for (var i = 0; i < variantRequests.Count; i++)
            series.Variants.Add(NewVariant(series.RaceSeriesId, variantRequests[i], variantRequests[i].DisplayOrder ?? i));

        _context.RaceSeries.Add(series);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created race series {SeriesName} ({SeriesId}) with {VariantCount} variant(s)",
            series.Name, series.RaceSeriesId, series.Variants.Count);

        return CreatedAtAction(nameof(GetById), new { id = series.RaceSeriesId }, new RaceSeriesSummaryDto
        {
            RaceSeriesId = series.RaceSeriesId,
            Name = series.Name,
            Description = series.Description,
            VariantCount = series.Variants.Count,
            RaceCount = 0,
            IsGrandPrixSeries = series.Variants.Any(v => v.IsGrandPrixByDefault),
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRaceSeriesRequest request)
    {
        var series = await _context.RaceSeries.FindAsync(id);
        if (series == null)
            return NotFound($"Race series {id} not found");

        var name = request.Name.Trim();
        if (string.IsNullOrEmpty(name))
            return BadRequest(new { message = "Series name is required" });
        if (await _context.RaceSeries.AnyAsync(s => s.Name == name && s.RaceSeriesId != id))
            return BadRequest(new { message = $"A series named \"{name}\" already exists" });

        series.Name = name;
        series.Description = request.Description;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Series updated" });
    }

    /// <summary>
    /// Merges one or more source series into a target series. Each source variant is moved to
    /// the target, or — if the target already has a variant with the same name — its races are
    /// folded into that variant. The (now-empty) source series are deleted. Admin/Manager only.
    /// </summary>
    [HttpPost("{targetId}/merge")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Merge(Guid targetId, [FromBody] MergeRaceSeriesRequest request)
    {
        var target = await _context.RaceSeries
            .Include(s => s.Variants).ThenInclude(v => v.Races)
            .FirstOrDefaultAsync(s => s.RaceSeriesId == targetId);
        if (target == null)
            return NotFound($"Race series {targetId} not found");

        var sourceIds = request.SourceSeriesIds.Where(id => id != targetId).Distinct().ToList();
        if (sourceIds.Count == 0)
            return BadRequest(new { message = "No source series to merge" });

        var sourceSeries = await _context.RaceSeries
            .Include(s => s.Variants).ThenInclude(v => v.Races)
            .Where(s => sourceIds.Contains(s.RaceSeriesId))
            .ToListAsync();

        var variantsMoved = 0;
        var variantsMerged = 0;
        foreach (var (owner, sourceVariant) in sourceSeries.SelectMany(s => s.Variants.Select(v => (s, v))).ToList())
        {
            var targetVariant = target.Variants.FirstOrDefault(v => v.Matches(sourceVariant.Name));
            if (targetVariant == null)
            {
                owner.Variants.Remove(sourceVariant);
                sourceVariant.RaceSeriesId = target.RaceSeriesId;
                target.Variants.Add(sourceVariant);
                variantsMoved++;
                continue;
            }

            var conflict = FoldVariantInto(sourceVariant, targetVariant);
            if (conflict != null)
                return BadRequest(new { message = conflict });
            variantsMerged++;
        }

        // Pending multi-variant uploads are held against the series itself (no race yet) -
        // re-point them so removing the source series doesn't cascade-delete them.
        await _context.UploadBatches
            .Where(b => b.RaceSeriesId != null && sourceIds.Contains(b.RaceSeriesId.Value))
            .ForEachAsync(b => b.RaceSeriesId = target.RaceSeriesId);

        _context.RaceSeries.RemoveRange(sourceSeries);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Merged {SourceCount} series into {TargetId}: {Moved} variant(s) moved, {Merged} merged",
            sourceSeries.Count, targetId, variantsMoved, variantsMerged);

        return Ok(new { seriesRemoved = sourceSeries.Count, variantsMoved, variantsMerged });
    }

    /// <summary>
    /// Add a variant to a series. Admin/Manager only.
    /// </summary>
    [HttpPost("{id}/variants")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceVariantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceVariantDto>> CreateVariant(Guid id, [FromBody] RaceVariantRequest request)
    {
        var series = await _context.RaceSeries.Include(s => s.Variants).FirstOrDefaultAsync(s => s.RaceSeriesId == id);
        if (series == null)
            return NotFound($"Race series {id} not found");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Variant name is required" });
        if (series.Variants.Any(v => v.Matches(request.Name)))
            return BadRequest(new { message = $"\"{request.Name.Trim()}\" is already a variant (or alias) in this series" });

        var displayOrder = request.DisplayOrder ?? (series.Variants.Count == 0 ? 0 : series.Variants.Max(v => v.DisplayOrder) + 1);
        var variant = NewVariant(id, request, displayOrder);
        _context.RaceVariants.Add(variant);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Added variant {VariantName} to series {SeriesName}", variant.Name, series.Name);

        return CreatedAtAction(nameof(GetById), new { id }, ToVariantDto(variant));
    }

    /// <summary>
    /// Update a variant's name, aliases, default Grand Prix status, order, or course records.
    /// Admin/Manager only.
    /// </summary>
    [HttpPut("variants/{variantId}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceVariantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceVariantDto>> UpdateVariant(Guid variantId, [FromBody] RaceVariantRequest request)
    {
        var variant = await _context.RaceVariants.FindAsync(variantId);
        if (variant == null)
            return NotFound($"Race variant {variantId} not found");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Variant name is required" });

        var siblings = await _context.RaceVariants
            .Where(v => v.RaceSeriesId == variant.RaceSeriesId && v.RaceVariantId != variantId)
            .ToListAsync();
        if (siblings.Any(v => v.Matches(request.Name)))
            return BadRequest(new { message = $"\"{request.Name.Trim()}\" is already a variant (or alias) in this series" });

        variant.Name = request.Name.Trim();
        variant.Aliases = NormalizeAliases(request.Aliases, variant.Name);
        variant.Description = request.Description;
        variant.IsGrandPrixByDefault = request.IsGrandPrixByDefault;
        variant.DisplayOrder = request.DisplayOrder ?? variant.DisplayOrder;
        variant.RecordTimeMale = request.RecordTimeMale;
        variant.RecordTimeFemale = request.RecordTimeFemale;
        variant.RecordHolderMale = request.RecordHolderMale;
        variant.RecordHolderFemale = request.RecordHolderFemale;
        await _context.SaveChangesAsync();

        return Ok(ToVariantDto(variant));
    }

    /// <summary>
    /// Delete a variant that has no races. Admin/Manager only.
    /// </summary>
    [HttpDelete("variants/{variantId}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteVariant(Guid variantId)
    {
        var variant = await _context.RaceVariants.Include(v => v.Races).FirstOrDefaultAsync(v => v.RaceVariantId == variantId);
        if (variant == null)
            return NotFound($"Race variant {variantId} not found");
        if (variant.Races.Count > 0)
            return BadRequest(new { message = "Cannot delete a variant that has races. Merge it into another variant instead." });

        _context.RaceVariants.Remove(variant);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Folds a duplicate variant (e.g. "Happy Trail") into another variant of the same series
    /// (e.g. "Happy Trails"): its races move over, its name becomes an alias of the target, and it
    /// is deleted. Fails if both variants have a race in the same year. Admin/Manager only.
    /// </summary>
    [HttpPost("variants/{sourceVariantId}/merge-into/{targetVariantId}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MergeVariant(Guid sourceVariantId, Guid targetVariantId)
    {
        if (sourceVariantId == targetVariantId)
            return BadRequest(new { message = "Cannot merge a variant into itself" });

        var variants = await _context.RaceVariants
            .Include(v => v.Races)
            .Where(v => v.RaceVariantId == sourceVariantId || v.RaceVariantId == targetVariantId)
            .ToListAsync();
        var source = variants.FirstOrDefault(v => v.RaceVariantId == sourceVariantId);
        var target = variants.FirstOrDefault(v => v.RaceVariantId == targetVariantId);
        if (source == null || target == null)
            return NotFound("Race variant not found");
        if (source.RaceSeriesId != target.RaceSeriesId)
            return BadRequest(new { message = "Variants must belong to the same series; merge the series first" });

        var racesMoved = source.Races.Count;
        var conflict = FoldVariantInto(source, target);
        if (conflict != null)
            return BadRequest(new { message = conflict });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Merged variant {Source} into {Target}, moving {RaceCount} race(s)",
            source.Name, target.Name, racesMoved);

        return Ok(new { racesMoved });
    }

    /// <summary>
    /// Moves <paramref name="source"/>'s races to <paramref name="target"/>, adds its name and
    /// aliases to the target's aliases, and marks it for deletion. Returns an error message
    /// (and changes nothing) if both have a race in the same year.
    /// </summary>
    private string? FoldVariantInto(RaceVariant source, RaceVariant target)
    {
        var conflictYears = source.Races.Select(r => r.Year).Intersect(target.Races.Select(r => r.Year)).OrderBy(y => y).ToList();
        if (conflictYears.Count > 0)
            return $"\"{source.Name}\" and \"{target.Name}\" both have races in {string.Join(", ", conflictYears)}; " +
                   "resolve those duplicates first";

        foreach (var race in source.Races.ToList())
        {
            race.RaceVariantId = target.RaceVariantId;
            target.Races.Add(race);
        }
        source.Races.Clear();

        target.Aliases = NormalizeAliases(target.Aliases.Append(source.Name).Concat(source.Aliases), target.Name);
        _context.RaceVariants.Remove(source);
        return null;
    }

    private static RaceVariant NewVariant(Guid seriesId, RaceVariantRequest request, int displayOrder)
    {
        var name = request.Name.Trim();
        return new RaceVariant
        {
            RaceVariantId = Guid.NewGuid(),
            RaceSeriesId = seriesId,
            Name = name,
            Aliases = NormalizeAliases(request.Aliases, name),
            Description = request.Description,
            IsGrandPrixByDefault = request.IsGrandPrixByDefault,
            DisplayOrder = displayOrder,
            RecordTimeMale = request.RecordTimeMale,
            RecordTimeFemale = request.RecordTimeFemale,
            RecordHolderMale = request.RecordHolderMale,
            RecordHolderFemale = request.RecordHolderFemale,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static List<string> NormalizeAliases(IEnumerable<string>? aliases, string name) =>
        (aliases ?? [])
            .Select(a => a.Trim())
            .Where(a => a.Length > 0 && !string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static RaceVariantDto ToVariantDto(RaceVariant v) => new()
    {
        RaceVariantId = v.RaceVariantId,
        RaceSeriesId = v.RaceSeriesId,
        Name = v.Name,
        Aliases = v.Aliases,
        Description = v.Description,
        IsGrandPrixByDefault = v.IsGrandPrixByDefault,
        DisplayOrder = v.DisplayOrder,
        RecordTimeMale = v.RecordTimeMale,
        RecordTimeFemale = v.RecordTimeFemale,
        RecordHolderMale = v.RecordHolderMale,
        RecordHolderFemale = v.RecordHolderFemale,
        RaceCount = v.Races.Count
    };
}

public class RaceSeriesSummaryDto
{
    public Guid RaceSeriesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int VariantCount { get; set; }
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
    public List<RaceVariantDto> Variants { get; set; } = new();
    public List<RaceSeriesInstanceDto> Races { get; set; } = new();
}

public class RaceVariantDto
{
    public Guid RaceVariantId { get; set; }
    public Guid RaceSeriesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public string? Description { get; set; }
    public bool IsGrandPrixByDefault { get; set; }
    public int DisplayOrder { get; set; }
    public TimeSpan? RecordTimeMale { get; set; }
    public TimeSpan? RecordTimeFemale { get; set; }
    public string? RecordHolderMale { get; set; }
    public string? RecordHolderFemale { get; set; }
    public int RaceCount { get; set; }
}

public class RaceSeriesInstanceDto
{
    public Guid RaceId { get; set; }
    public int Year { get; set; }
    public DateOnly Date { get; set; }
    public Guid RaceVariantId { get; set; }

    /// <summary>Variant name; null when the series has only one variant.</summary>
    public string? CourseVariant { get; set; }
    public bool IsGrandPrixRace { get; set; }
    public int ResultsCount { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public int VariantDisplayOrder { get; set; }
}

public class CreateRaceSeriesRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The series' variants. Leave empty for a single-course series.</summary>
    public List<RaceVariantRequest> Variants { get; set; } = new();

    /// <summary>Whether the auto-created "Standard" variant counts toward the Grand Prix (only used when <see cref="Variants"/> is empty).</summary>
    public bool IsGrandPrix { get; set; }
}

public class UpdateRaceSeriesRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RaceVariantRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string>? Aliases { get; set; }
    public string? Description { get; set; }
    public bool IsGrandPrixByDefault { get; set; }
    public int? DisplayOrder { get; set; }
    public TimeSpan? RecordTimeMale { get; set; }
    public TimeSpan? RecordTimeFemale { get; set; }
    public string? RecordHolderMale { get; set; }
    public string? RecordHolderFemale { get; set; }
}

public class MergeRaceSeriesRequest
{
    public List<Guid> SourceSeriesIds { get; set; } = new();
}
