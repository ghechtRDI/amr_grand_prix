using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Common;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.ResultsProcessing;
using AmrGrandPrix.API.Services.Runners;

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
    private readonly IRunnerMergeService _runnerMergeService;
    private readonly ILogger<RunnersController> _logger;

    public RunnersController(
        ApplicationDbContext context,
        IRunnerMatchingService runnerMatchingService,
        IRunnerMergeService runnerMergeService,
        ILogger<RunnersController> logger)
    {
        _context = context;
        _runnerMatchingService = runnerMatchingService;
        _runnerMergeService = runnerMergeService;
        _logger = logger;
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

            if (request.AlternateNames != null && request.AlternateNames.Count > UpdateRunnerRequest.MaxAlternateNames)
            {
                return BadRequest($"Up to {UpdateRunnerRequest.MaxAlternateNames} alternate names are allowed");
            }

            // Update fields
            runner.FirstName = request.FirstName;
            runner.LastName = request.LastName;
            runner.Gender = request.Gender;
            runner.DateOfBirth = request.DateOfBirth;
            runner.Email = request.Email;
            if (request.PreferredName != null)
                runner.PreferredName = string.IsNullOrWhiteSpace(request.PreferredName) ? null : request.PreferredName.Trim();
            if (request.AlternateNames != null)
                runner.AlternateNames = RunnerNames.Normalize(request.AlternateNames);
            runner.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var runnerDto = new RunnerDto
            {
                RunnerId = runner.RunnerId,
                FirstName = runner.FirstName,
                LastName = runner.LastName,
                Gender = runner.Gender
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
    /// Public athlete directory: every runner with their current age, result count, and best
    /// Open-division Grand Prix season total. Sorting and searching happen client-side — the
    /// runner table is a few thousand rows. Deliberately excludes date of birth, birth year,
    /// email, and alternate names.
    /// </summary>
    [HttpGet("directory")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<AthleteDirectoryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AthleteDirectoryEntryDto>>> GetAthleteDirectory()
    {
        var runners = await _context.Runners
            .AsNoTracking()
            .Select(r => new { r.RunnerId, r.FirstName, r.LastName, r.Gender, r.DateOfBirth, r.EstimatedBirthYear })
            .ToListAsync();

        var resultCounts = await _context.RaceResults
            .GroupBy(rr => rr.RunnerId)
            .Select(g => new { RunnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RunnerId, g => g.Count);

        // Open division only: age-division totals are on a 5/4/3/2/1 scale and would rank
        // alongside Open totals misleadingly. Includes the in-progress (unfinalized) season.
        var topScores = (await _context.GrandPrixStandings
                .Where(s => s.AgeCategory == null)
                .Select(s => new { s.RunnerId, s.Year, s.TotalPoints })
                .ToListAsync())
            .GroupBy(s => s.RunnerId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.TotalPoints).ThenByDescending(s => s.Year).First());

        var today = DateOnly.FromDateTime(DateTime.Today);

        return Ok(runners
            .Select(r =>
            {
                var ageSource = new Runner { DateOfBirth = r.DateOfBirth, EstimatedBirthYear = r.EstimatedBirthYear };
                var top = topScores.GetValueOrDefault(r.RunnerId);
                return new AthleteDirectoryEntryDto
                {
                    RunnerId = r.RunnerId,
                    FullName = $"{r.FirstName} {r.LastName}",
                    Gender = r.Gender,
                    CurrentAge = AgeCalculator.GetRunnerAge(ageSource, today),
                    IsAgeEstimated = AgeCalculator.IsAgeEstimated(ageSource),
                    ResultCount = resultCounts.GetValueOrDefault(r.RunnerId),
                    TopGrandPrixScore = top?.TotalPoints,
                    TopGrandPrixYear = top?.Year
                };
            })
            .OrderByDescending(a => a.TopGrandPrixScore.HasValue)
            .ThenByDescending(a => a.TopGrandPrixScore)
            .ThenBy(a => a.FullName)
            .ToList());
    }

    /// <summary>
    /// One runner's admin record (the same shape as the <c>manage</c> list), for opening the edit
    /// dialog from public pages.
    /// </summary>
    [HttpGet("{id}/manage")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RunnerAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunnerAdminDto>> GetRunnerForManagement(Guid id)
    {
        var summaries = await _runnerMergeService.GetAdminSummariesAsync([id]);
        return summaries.TryGetValue(id, out var runner)
            ? Ok(runner)
            : NotFound($"Runner with ID {id} not found");
    }

    /// <summary>
    /// Admin runner list with alternate names, birth info, result counts, and linked accounts.
    /// The search matches legal, preferred, and alternate names.
    /// </summary>
    [HttpGet("manage")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(List<RunnerAdminDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RunnerAdminDto>>> GetRunnersForManagement(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 100)
    {
        // Alternate names are a Postgres array, so the name search runs in memory — the runner
        // table is small enough (a few thousand rows) for that to be cheap.
        var runners = await _context.Runners
            .AsNoTracking()
            .Select(r => new { r.RunnerId, r.FirstName, r.LastName, r.PreferredName, r.AlternateNames })
            .ToListAsync();

        var terms = (search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ids = runners
            .Where(r =>
            {
                var haystack = string.Join(" ",
                    new[] { r.FirstName, r.LastName, r.PreferredName ?? string.Empty }.Concat(r.AlternateNames));
                return terms.All(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
            })
            .OrderBy(r => r.LastName).ThenBy(r => r.FirstName)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(r => r.RunnerId)
            .ToList();

        var summaries = await _runnerMergeService.GetAdminSummariesAsync(ids);
        return Ok(ids.Select(id => summaries[id]).ToList());
    }

    /// <summary>
    /// Pairs of runner records that are probably the same person (default: at least 75% match),
    /// highest confidence first.
    /// </summary>
    [HttpGet("potential-duplicates")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(List<PotentialDuplicateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PotentialDuplicateDto>>> GetPotentialDuplicates(
        [FromQuery] double minConfidence = 0.75)
    {
        return Ok(await _runnerMergeService.FindPotentialDuplicatesAsync(Math.Clamp(minConfidence, 0.5, 1.0)));
    }

    /// <summary>Pairs marked as "not a match", most recently dismissed first.</summary>
    [HttpGet("not-duplicates")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(List<DismissedDuplicateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DismissedDuplicateDto>>> GetDismissedDuplicates()
    {
        return Ok(await _runnerMergeService.GetDismissedDuplicatesAsync());
    }

    /// <summary>
    /// Marks two runners as different people so the pair no longer shows as a possible duplicate.
    /// </summary>
    [HttpPost("not-duplicates")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DismissDuplicate([FromBody] RunnerPairRequest request)
    {
        if (request.RunnerAId == request.RunnerBId)
            return BadRequest(new { message = "Pick two different runners." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _runnerMergeService.DismissDuplicateAsync(request.RunnerAId, request.RunnerBId, userId)
            ? NoContent()
            : NotFound(new { message = "Runner not found" });
    }

    /// <summary>Puts a pair marked as "not a match" back on the possible duplicates list.</summary>
    [HttpDelete("not-duplicates")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreDuplicate([FromQuery] Guid runnerAId, [FromQuery] Guid runnerBId)
    {
        return await _runnerMergeService.RestoreDuplicateAsync(runnerAId, runnerBId)
            ? NoContent()
            : NotFound(new { message = "That pair isn't marked as not a match." });
    }

    /// <summary>
    /// What merging <paramref name="sourceId"/> into <paramref name="id"/> would do — results moved,
    /// resulting names, standings recalculated, and anything that blocks it. Changes nothing.
    /// </summary>
    [HttpGet("{id}/merge-preview/{sourceId}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RunnerMergePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunnerMergePreviewDto>> GetMergePreview(Guid id, Guid sourceId)
    {
        var preview = await _runnerMergeService.PreviewMergeAsync(id, sourceId);
        return preview == null ? NotFound("Runner not found") : Ok(preview);
    }

    /// <summary>
    /// Merges the source runner into this one: every result moves over, both names become
    /// alternate names, the source is deleted, and affected Grand Prix standings are recalculated.
    /// Returns 409 if a finalized Grand Prix year would change, 400 for any other blocking issue.
    /// </summary>
    [HttpPost("{id}/merge")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RunnerMergeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RunnerMergeResultDto>> MergeRunner(Guid id, [FromBody] MergeRunnersRequest request)
    {
        var preview = await _runnerMergeService.PreviewMergeAsync(id, request.SourceRunnerId);
        if (preview == null)
            return NotFound(new { message = "Runner not found" });

        if (!preview.CanMerge)
        {
            var message = string.Join(" ", preview.BlockingIssues);
            return preview.FinalizedYears.Count > 0
                ? Conflict(new { message })
                : BadRequest(new { message });
        }

        return Ok(await _runnerMergeService.MergeAsync(id, request.SourceRunnerId));
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
/// One row of the public athlete directory — no contact details, date of birth, or birth year.
/// </summary>
public class AthleteDirectoryEntryDto
{
    public Guid RunnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public int? CurrentAge { get; set; }
    public bool IsAgeEstimated { get; set; }
    public int ResultCount { get; set; }

    /// <summary>Best Open-division Grand Prix season total; null if the runner never scored.</summary>
    public int? TopGrandPrixScore { get; set; }

    /// <summary>The season <see cref="TopGrandPrixScore"/> was earned in.</summary>
    public int? TopGrandPrixYear { get; set; }
}

/// <summary>
/// Request for updating an existing runner
/// </summary>
public class UpdateRunnerRequest
{
    /// <summary>
    /// More than a user profile's 3: a merged runner collects the names of every record folded
    /// into it.
    /// </summary>
    public const int MaxAlternateNames = 20;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Email { get; set; }

    /// <summary>Null leaves it unchanged; an empty string clears it.</summary>
    public string? PreferredName { get; set; }

    /// <summary>Null leaves them unchanged; an empty list clears them.</summary>
    public List<string>? AlternateNames { get; set; }
}
