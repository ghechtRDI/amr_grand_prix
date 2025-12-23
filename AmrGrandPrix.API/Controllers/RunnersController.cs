using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                r.LastName.ToLower().Contains(searchLower) ||
                r.Email != null && r.Email.ToLower().Contains(searchLower));
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
                DateOfBirth = r.DateOfBirth,
                Email = r.Email
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
            Email = runner.Email,
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
                    RaceName = rr.Race.Name,
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
        int? age = null;
        if (request.DateOfBirth.HasValue)
        {
            var today = DateTime.Today;
            age = today.Year - request.DateOfBirth.Value.Year;
            if (request.DateOfBirth.Value.Date > today.AddYears(-age.Value))
            {
                age--;
            }
        }

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

                int? age = null;
                if (request.DateOfBirth.HasValue)
                {
                    var today = DateTime.Today;
                    age = today.Year - request.DateOfBirth.Value.Year;
                    if (request.DateOfBirth.Value.Date > today.AddYears(-age.Value))
                    {
                        age--;
                    }
                }

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
                DateOfBirth = runner.DateOfBirth,
                Email = runner.Email
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
                DateOfBirth = runner.DateOfBirth,
                Email = runner.Email
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
                RaceName = rr.Race.Name,
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
}

/// <summary>
/// Request for checking duplicate runners
/// </summary>
public class CheckDuplicatesRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
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
    public DateTime? DateOfBirth { get; set; }
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
    public DateTime? DateOfBirth { get; set; }
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
