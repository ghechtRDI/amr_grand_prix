using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.ResultsProcessing;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Controller for managing runners
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RunnersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRunnerMatchingService _runnerMatchingService;
    private readonly ILogger<RunnersController> _logger;

    public RunnersController(
        ApplicationDbContext context,
        IRunnerMatchingService runnerMatchingService,
        ILogger<RunnersController> logger)
    {
        _context = context;
        _runnerMatchingService = runnerMatchingService;
        _logger = logger;
    }

    /// <summary>
    /// Search and list runners
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RunnerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RunnerDto>>> GetRunners(
        [FromQuery] string? search = null,
        [FromQuery] int? limit = 100,
        [FromQuery] int? offset = 0)
    {
        var query = _context.Runners.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(r =>
                r.FirstName.ToLower().Contains(searchLower) ||
                r.LastName.ToLower().Contains(searchLower));
        }

        // Apply pagination
        query = query.OrderBy(r => r.LastName).ThenBy(r => r.FirstName);

        if (offset.HasValue && offset.Value > 0)
        {
            query = query.Skip(offset.Value);
        }

        if (limit.HasValue && limit.Value > 0)
        {
            query = query.Take(limit.Value);
        }

        var runners = await query
            .Select(r => new RunnerDto
            {
                RunnerId = r.RunnerId,
                FirstName = r.FirstName,
                LastName = r.LastName,
                Gender = r.Gender,
                DateOfBirth = r.DateOfBirth
            })
            .ToListAsync();

        return Ok(runners);
    }

    /// <summary>
    /// Get a specific runner by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RunnerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunnerDetailDto>> GetRunner(Guid id)
    {
        var runner = await _context.Runners
            .Include(r => r.Results)
                .ThenInclude(rr => rr.Race)
                .ThenInclude(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
            .Include(r => r.GrandPrixStandings)
            .FirstOrDefaultAsync(r => r.RunnerId == id);

        if (runner == null)
        {
            return NotFound($"Runner with ID {id} not found");
        }

        var runnerDto = new RunnerDetailDto
        {
            RunnerId = runner.RunnerId,
            FirstName = runner.FirstName,
            LastName = runner.LastName,
            Gender = runner.Gender,
            DateOfBirth = runner.DateOfBirth,
            TotalRaces = runner.Results.Count,
            GrandPrixYears = runner.GrandPrixStandings
                .Select(s => s.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList(),
            RecentResults = runner.Results
                .OrderByDescending(rr => rr.Race.Date)
                .Take(10)
                .Select(rr => new RaceResultDto
                {
                    ResultId = rr.ResultId,
                    RaceId = rr.RaceId,
                    RaceName = RaceProjections.DisplayName(rr.Race),
                    RaceDate = rr.Race.Date,
                    Place = rr.Place,
                    PlaceGender = rr.PlaceGender,
                    Time = rr.Time,
                    Age = rr.Age,
                    Gender = rr.Gender,
                    Status = rr.Status
                })
                .ToList()
        };

        return Ok(runnerDto);
    }

    /// <summary>
    /// Check for potential duplicate runners before creating
    /// </summary>
    [HttpPost("check-duplicates")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(CheckDuplicatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckDuplicatesResponse>> CheckDuplicates([FromBody] CheckDuplicatesRequest request)
    {
        var fullName = $"{request.FirstName} {request.LastName}";

        // Calculate age if date of birth provided
        int? age = request.DateOfBirth.HasValue
            ? AgeCalculator.CalculateAge(request.DateOfBirth.Value, DateOnly.FromDateTime(DateTime.Today))
            : null;

        // Find potential matches using fuzzy matching
        var matches = await _runnerMatchingService.FindMatchesAsync(
            fullName,
            age,
            request.Gender);

        var matchDtos = matches.Select(m => RunnerMatchDto.FromRunnerMatch(m)).ToList();

        var response = new CheckDuplicatesResponse
        {
            HasPotentialDuplicates = matchDtos.Any(),
            PotentialMatches = matchDtos,
            HighConfidenceMatch = matches.Any(m => m.Confidence >= 0.95)
        };

        return Ok(response);
    }

    /// <summary>
    /// Create a new runner
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RunnerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateRunnerConflictResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunnerDto>> CreateRunner([FromBody] CreateRunnerRequest request)
    {
        try
        {
            // Check for potential duplicates using fuzzy matching (unless explicitly skipped)
            if (!request.SkipDuplicateCheck)
            {
                var fullName = $"{request.FirstName} {request.LastName}";

                int? age = request.DateOfBirth.HasValue
                    ? AgeCalculator.CalculateAge(request.DateOfBirth.Value, DateOnly.FromDateTime(DateTime.Today))
                    : null;

                var matches = await _runnerMatchingService.FindMatchesAsync(
                    fullName,
                    age,
                    request.Gender);

                // If we found potential duplicates, return them
                if (matches.Any())
                {
                    var matchDtos = matches.Select(m => RunnerMatchDto.FromRunnerMatch(m)).ToList();

                    var conflictResponse = new CreateRunnerConflictResponse
                    {
                        Message = "Potential duplicate runners found. Review matches or set skipDuplicateCheck=true to force creation.",
                        PotentialMatches = matchDtos,
                        HighConfidenceMatch = matches.Any(m => m.Confidence >= 0.95)
                    };

                    return Conflict(conflictResponse);
                }
            }

            // No duplicates found or check was skipped, create the runner
            var runner = new Runner
            {
                RunnerId = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Runners.Add(runner);
            await _context.SaveChangesAsync();

            var runnerDto = new RunnerDto
            {
                RunnerId = runner.RunnerId,
                FirstName = runner.FirstName,
                LastName = runner.LastName,
                Gender = runner.Gender,
                DateOfBirth = runner.DateOfBirth
            };

            _logger.LogInformation(
                "Created new runner: {FirstName} {LastName} ({RunnerId})",
                runner.FirstName, runner.LastName, runner.RunnerId);

            return CreatedAtAction(nameof(GetRunner), new { id = runner.RunnerId }, runnerDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating runner");
            return BadRequest($"Error creating runner: {ex.Message}");
        }
    }

    /// <summary>
    /// Update an existing runner
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RunnerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RunnerDto>> UpdateRunner(Guid id, [FromBody] UpdateRunnerRequest request)
    {
        try
        {
            var runner = await _context.Runners.FindAsync(id);
            if (runner == null)
            {
                return NotFound($"Runner with ID {id} not found");
            }

            // Update fields
            runner.FirstName = request.FirstName;
            runner.LastName = request.LastName;
            runner.Gender = request.Gender;
            runner.DateOfBirth = request.DateOfBirth;
            runner.Email = request.Email;
            runner.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var runnerDto = new RunnerDto
            {
                RunnerId = runner.RunnerId,
                FirstName = runner.FirstName,
                LastName = runner.LastName,
                Gender = runner.Gender,
                DateOfBirth = runner.DateOfBirth
            };

            _logger.LogInformation(
                "Updated runner: {FirstName} {LastName} ({RunnerId})",
                runner.FirstName, runner.LastName, runner.RunnerId);

            return Ok(runnerDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating runner {RunnerId}", id);
            return BadRequest($"Error updating runner: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a runner (only if no results exist)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteRunner(Guid id)
    {
        try
        {
            var runner = await _context.Runners
                .Include(r => r.Results)
                .FirstOrDefaultAsync(r => r.RunnerId == id);

            if (runner == null)
            {
                return NotFound($"Runner with ID {id} not found");
            }

            // Prevent deletion if results exist
            if (runner.Results.Any())
            {
                return BadRequest("Cannot delete runner with existing race results");
            }

            _context.Runners.Remove(runner);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deleted runner: {FirstName} {LastName} ({RunnerId})",
                runner.FirstName, runner.LastName, runner.RunnerId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting runner {RunnerId}", id);
            return BadRequest($"Error deleting runner: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all race results for a specific runner
    /// </summary>
    [HttpGet("{id}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RaceResultDto>>> GetRunnerResults(Guid id)
    {
        var runner = await _context.Runners.FindAsync(id);
        if (runner == null)
        {
            return NotFound($"Runner with ID {id} not found");
        }

        var results = await _context.RaceResults
            .Include(rr => rr.Race)
            .Where(rr => rr.RunnerId == id)
            .OrderByDescending(rr => rr.Race.Date)
            .Select(rr => new RaceResultDto
            {
                ResultId = rr.ResultId,
                RaceId = rr.RaceId,
                RaceName = RaceProjections.DisplayName(rr.Race.RaceVariant.RaceSeries.Name, rr.Race.RaceVariant.Name, rr.Race.RaceVariant.RaceSeries.Variants.Count),
                RaceDate = rr.Race.Date,
                Place = rr.Place,
                PlaceGender = rr.PlaceGender,
                PlaceAgeCategory = rr.PlaceAgeCategory,
                Time = rr.Time,
                Age = rr.Age,
                Gender = rr.Gender,
                Status = rr.Status,
                Bib = rr.Bib,
                Notes = rr.Notes
            })
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>
    /// Everything the public runner page needs in one call: basic info, a summary per race series
    /// the runner has results in, and every result with overall/gender/age-group places and a
    /// personal-record flag. Places are computed from finish time across the whole race rather
    /// than trusted from the stored place fields, which can be section-relative (e.g. a per-gender
    /// results sheet) — the same approach the race results page takes client-side.
    /// </summary>
    [HttpGet("{id}/profile")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RunnerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunnerProfileDto>> GetRunnerProfile(Guid id)
    {
        var runner = await _context.Runners.FindAsync(id);
        if (runner == null)
        {
            return NotFound($"Runner with ID {id} not found");
        }

        var ownResults = await _context.RaceResults
            .Where(rr => rr.RunnerId == id)
            .Select(rr => new
            {
                rr.ResultId,
                rr.RaceId,
                RaceName = rr.Race.RaceVariant.RaceSeries.Name,
                rr.Race.RaceVariantId,
                CourseVariant = rr.Race.RaceVariant.RaceSeries.Variants.Count > 1 ? rr.Race.RaceVariant.Name : null,
                rr.Race.RaceVariant.RaceSeriesId,
                RaceSeriesName = rr.Race.RaceVariant.RaceSeries.Name,
                rr.Race.Date,
                rr.Race.Year,
                rr.Race.IsGrandPrixRace,
                rr.Time,
                rr.Status,
                rr.Gender,
                rr.Age,
                rr.AgeCategory
            })
            .ToListAsync();

        var raceIds = ownResults.Select(r => r.RaceId).Distinct().ToList();
        var finishersByRace = (await _context.RaceResults
                .Where(rr => raceIds.Contains(rr.RaceId) &&
                             rr.Status == ResultStatus.Finished &&
                             rr.Time != null && rr.Time > TimeSpan.Zero)
                .Select(rr => new { rr.RaceId, rr.Gender, rr.AgeCategory, Time = rr.Time!.Value })
                .ToListAsync())
            .ToLookup(f => f.RaceId);

        static bool IsFinish(ResultStatus status, TimeSpan? time) =>
            status == ResultStatus.Finished && time.HasValue && time.Value > TimeSpan.Zero;

        var finishesByCourse = ownResults
            .Where(r => IsFinish(r.Status, r.Time))
            .GroupBy(r => r.RaceVariantId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Best: g.Min(r => r.Time!.Value)));

        var results = ownResults
            .OrderByDescending(r => r.Date)
            .Select(r =>
            {
                var dto = new RunnerProfileResultDto
                {
                    ResultId = r.ResultId,
                    RaceId = r.RaceId,
                    RaceName = r.RaceName,
                    RaceVariantId = r.RaceVariantId,
                    CourseVariant = r.CourseVariant,
                    RaceSeriesId = r.RaceSeriesId,
                    RaceSeriesName = r.RaceSeriesName,
                    Date = r.Date,
                    Year = r.Year,
                    IsGrandPrixRace = r.IsGrandPrixRace,
                    Time = r.Time,
                    Status = r.Status,
                    Age = r.Age,
                    AgeCategory = r.AgeCategory
                };

                if (!IsFinish(r.Status, r.Time))
                    return dto;

                var time = r.Time!.Value;
                var finishers = finishersByRace[r.RaceId].ToList();
                var sameGender = finishers.Where(f => f.Gender == r.Gender).ToList();

                // Standard competition ranking: tied times share a place.
                dto.OverallPlace = finishers.Count(f => f.Time < time) + 1;
                dto.OverallFinishers = finishers.Count;
                dto.GenderPlace = sameGender.Count(f => f.Time < time) + 1;
                dto.GenderFinishers = sameGender.Count;

                // Relative to the runner's own gender winner, so it's comparable year to year
                // regardless of course conditions or who showed up in the other fields.
                var winnerTime = sameGender.Min(f => f.Time);
                dto.PercentBehindWinner = Math.Round((time - winnerTime) / winnerTime * 100, 1);

                var ageAtRace = r.Age ?? AgeCalculator.GetRunnerAge(runner, r.Date);
                dto.AgeGradedTime = AgeGrading.GradeTime(time, ageAtRace, r.Gender);

                if (!string.IsNullOrEmpty(r.AgeCategory))
                {
                    var sameAgeGroup = sameGender.Where(f => f.AgeCategory == r.AgeCategory).ToList();
                    dto.AgeGroupPlace = sameAgeGroup.Count(f => f.Time < time) + 1;
                    dto.AgeGroupFinishers = sameAgeGroup.Count;
                }

                // A PR is only meaningful once there's more than one finish to compare against.
                var course = finishesByCourse[r.RaceVariantId];
                dto.IsPersonalRecord = course.Count > 1 && time == course.Best;

                return dto;
            })
            .ToList();

        var series = results
            .GroupBy(r => r.RaceSeriesId)
            .Select(g => new RunnerSeriesSummaryDto
            {
                RaceSeriesId = g.Key,
                Name = g.First().RaceSeriesName,
                ResultCount = g.Count(),
                FinishCount = g.Count(r => r.OverallPlace.HasValue),
                FirstYear = g.Min(r => r.Year),
                LastYear = g.Max(r => r.Year),
                Variants = g
                    .GroupBy(r => (r.RaceVariantId, r.CourseVariant))
                    .Select(v => new RunnerSeriesVariantDto
                    {
                        RaceVariantId = v.Key.RaceVariantId,
                        CourseVariant = v.Key.CourseVariant,
                        ResultCount = v.Count(),
                        BestTime = v.Where(r => r.OverallPlace.HasValue).Min(r => r.Time)
                    })
                    .OrderByDescending(v => v.ResultCount)
                    .ToList()
            })
            .OrderByDescending(s => s.ResultCount)
            .ThenBy(s => s.Name)
            .ToList();

        var standings = await _context.GrandPrixStandings
            .Where(s => s.RunnerId == id)
            .ToListAsync();

        var standingYears = standings.Select(s => s.Year).Distinct().ToList();
        var divisionSizes = (await _context.GrandPrixStandings
                .Where(s => standingYears.Contains(s.Year))
                .GroupBy(s => new { s.Year, s.Division, s.AgeCategory })
                .Select(g => new { g.Key.Year, g.Key.Division, g.Key.AgeCategory, Count = g.Count() })
                .ToListAsync())
            .ToDictionary(g => (g.Year, g.Division, g.AgeCategory), g => g.Count);

        var finalizedYears = (await _context.GrandPrixSeasons
                .Where(s => standingYears.Contains(s.Year) && s.IsFinalized)
                .Select(s => s.Year)
                .ToListAsync())
            .ToHashSet();

        var grandPrixHistory = standings
            .OrderByDescending(s => s.Year)
            .ThenBy(s => s.AgeCategory != null) // Open division first
            .Select(s => new RunnerGrandPrixStandingDto
            {
                Year = s.Year,
                Division = s.Division,
                AgeCategory = s.AgeCategory,
                Rank = s.Rank,
                DivisionSize = divisionSizes.GetValueOrDefault((s.Year, s.Division, s.AgeCategory)),
                TotalPoints = s.TotalPoints,
                RacesCompleted = s.RacesCompleted,
                RacesCounted = s.RacesCounted,
                RunTheGamutQualified = s.RunTheGamutQualified,
                IsFinalized = finalizedYears.Contains(s.Year)
            })
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);

        return Ok(new RunnerProfileDto
        {
            RunnerId = runner.RunnerId,
            FirstName = runner.FirstName,
            LastName = runner.LastName,
            FullName = runner.FullName,
            Gender = runner.Gender,
            CurrentAge = AgeCalculator.GetRunnerAge(runner, today),
            IsAgeEstimated = AgeCalculator.IsAgeEstimated(runner),
            Series = series,
            Results = results,
            GrandPrixHistory = grandPrixHistory
        });
    }
}

/// <summary>
/// Public runner page payload — deliberately excludes contact details and date of birth.
/// </summary>
public class RunnerProfileDto
{
    public Guid RunnerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public int? CurrentAge { get; set; }
    public bool IsAgeEstimated { get; set; }
    public List<RunnerSeriesSummaryDto> Series { get; set; } = new();
    public List<RunnerProfileResultDto> Results { get; set; } = new();
    public List<RunnerGrandPrixStandingDto> GrandPrixHistory { get; set; } = new();
}

public class RunnerGrandPrixStandingDto
{
    public int Year { get; set; }
    public Division Division { get; set; }
    public string? AgeCategory { get; set; }
    public int Rank { get; set; }
    public int DivisionSize { get; set; }
    public int TotalPoints { get; set; }
    public int RacesCompleted { get; set; }
    public int RacesCounted { get; set; }
    public bool RunTheGamutQualified { get; set; }

    /// <summary>False while the season is still in progress (standings are tentative).</summary>
    public bool IsFinalized { get; set; }
}

public class RunnerSeriesSummaryDto
{
    public Guid RaceSeriesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public int FinishCount { get; set; }
    public int FirstYear { get; set; }
    public int LastYear { get; set; }
    public List<RunnerSeriesVariantDto> Variants { get; set; } = new();
}

public class RunnerSeriesVariantDto
{
    public Guid RaceVariantId { get; set; }
    public string? CourseVariant { get; set; }
    public int ResultCount { get; set; }
    public TimeSpan? BestTime { get; set; }
}

public class RunnerProfileResultDto
{
    public Guid ResultId { get; set; }
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public Guid RaceVariantId { get; set; }
    public string? CourseVariant { get; set; }
    public Guid RaceSeriesId { get; set; }
    public string RaceSeriesName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int Year { get; set; }
    public bool IsGrandPrixRace { get; set; }
    public TimeSpan? Time { get; set; }
    public ResultStatus Status { get; set; }
    public int? Age { get; set; }
    public string? AgeCategory { get; set; }
    public int? OverallPlace { get; set; }
    public int? OverallFinishers { get; set; }
    public int? GenderPlace { get; set; }
    public int? GenderFinishers { get; set; }
    public int? AgeGroupPlace { get; set; }
    public int? AgeGroupFinishers { get; set; }
    public bool IsPersonalRecord { get; set; }

    /// <summary>Percent slower than the winner of the runner's gender, e.g. 12.5 = 12.5% back.</summary>
    public double? PercentBehindWinner { get; set; }

    /// <summary>Open-age equivalent time; only set at or above <see cref="AgeGrading.MinimumAge"/>.</summary>
    public TimeSpan? AgeGradedTime { get; set; }
}

/// <summary>
/// Request for checking duplicate runners
/// </summary>
public class CheckDuplicatesRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}

/// <summary>
/// Response from duplicate check
/// </summary>
public class CheckDuplicatesResponse
{
    public bool HasPotentialDuplicates { get; set; }
    public List<RunnerMatchDto> PotentialMatches { get; set; } = new();
    public bool HighConfidenceMatch { get; set; }
}

/// <summary>
/// Response when creating a runner finds potential duplicates
/// </summary>
public class CreateRunnerConflictResponse
{
    public string Message { get; set; } = string.Empty;
    public List<RunnerMatchDto> PotentialMatches { get; set; } = new();
    public bool HighConfidenceMatch { get; set; }
}

/// <summary>
/// Request for creating a new runner
/// </summary>
public class CreateRunnerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Email { get; set; }
    public bool SkipDuplicateCheck { get; set; } = false;
}

/// <summary>
/// Request for updating an existing runner
/// </summary>
public class UpdateRunnerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Detailed runner information with results
/// </summary>
public class RunnerDetailDto : RunnerDto
{
    public new int TotalRaces { get; set; }
    public List<int> GrandPrixYears { get; set; } = new();
    public List<RaceResultDto> RecentResults { get; set; } = new();
}
