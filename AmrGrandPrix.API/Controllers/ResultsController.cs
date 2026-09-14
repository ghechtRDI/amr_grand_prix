using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.LlmExtraction;
using AmrGrandPrix.API.Services.ResultsProcessing;
using AmrGrandPrix.API.Services.GrandPrix;

namespace AmrGrandPrix.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class ResultsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILlmExtractionService _llmExtractionService;
    private readonly IResultsProcessingService _resultsProcessingService;
    private readonly IRunnerMatchingService _runnerMatchingService;
    private readonly IGrandPrixCalculationService _grandPrixCalculationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ResultsController> _logger;

    public ResultsController(
        ApplicationDbContext context,
        ILlmExtractionService llmExtractionService,
        IResultsProcessingService resultsProcessingService,
        IRunnerMatchingService runnerMatchingService,
        IGrandPrixCalculationService grandPrixCalculationService,
        UserManager<ApplicationUser> userManager,
        ILogger<ResultsController> logger)
    {
        _context                  = context;
        _llmExtractionService     = llmExtractionService;
        _resultsProcessingService = resultsProcessingService;
        _runnerMatchingService    = runnerMatchingService;
        _grandPrixCalculationService = grandPrixCalculationService;
        _userManager              = userManager;
        _logger                   = logger;
    }

    /// <summary>Upload a race results file (PDF, CSV, or Excel) and extract results via LLM.</summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadResultsResponse>> UploadResults(
        [FromForm] UploadResultsRequest request,
        CancellationToken ct)
    {
        try
        {
            var race = await _context.Races.FindAsync([request.RaceId], ct);
            if (race == null)
                return NotFound($"Race with ID {request.RaceId} not found");

            if (request.File == null || request.File.Length == 0)
                return BadRequest("No file uploaded");

            if (request.File.Length > 10 * 1024 * 1024)
                return BadRequest("File size exceeds 10MB limit");

            var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();

            // LLM extraction
            ExtractionResult extraction;
            using (var stream = request.File.OpenReadStream())
                extraction = await _llmExtractionService.ExtractAsync(stream, request.File.FileName, ct);

            var processedResults = await _resultsProcessingService.ProcessResultsAsync(extraction.Sections);
            await MatchRunnersAsync(processedResults);

            var user = await _userManager.GetUserAsync(User);
            var uploadBatch = new UploadBatch
            {
                UploadBatchId    = Guid.NewGuid(),
                RaceId           = request.RaceId,
                FileName         = request.File.FileName,
                FileType         = ext switch
                {
                    ".csv" or ".txt" => FileType.CSV,
                    ".xlsx" or ".xls" => FileType.Excel,
                    ".pdf"           => FileType.PDF,
                    _                => FileType.CSV
                },
                RecordsUploaded  = processedResults.Count,
                UploadedBy       = user?.Id ?? "system",
                UploadedAt       = DateTime.UtcNow,
                Status           = UploadStatus.Pending,
                RawLlmJson       = extraction.RawModelJson,
                LlmModel         = extraction.LlmModel,
                LlmInputTokens   = extraction.InputTokens,
                LlmOutputTokens  = extraction.OutputTokens
            };

            _context.UploadBatches.Add(uploadBatch);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "File {FileName} uploaded for race {RaceId} by {UserId}. Extracted {Sections} section(s), {Rows} rows. Tokens: {In}in/{Out}out",
                request.File.FileName, request.RaceId, user?.Id,
                extraction.Sections.Count, processedResults.Count,
                extraction.InputTokens, extraction.OutputTokens);

            return Ok(BuildResponse(uploadBatch.UploadBatchId, processedResults));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading results file");
            return BadRequest($"Error processing file: {ex.Message}");
        }
    }

    /// <summary>Re-validate corrected results data.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(UploadResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadResultsResponse>> ValidateResults(
        [FromBody] ValidateResultsRequest request)
    {
        try
        {
            var uploadBatch = await _context.UploadBatches.FindAsync(request.UploadBatchId);
            if (uploadBatch == null)
                return NotFound($"Upload batch {request.UploadBatchId} not found");

            foreach (var result in request.Results)
                result.ValidationIssues = _resultsProcessingService.ValidateRow(result);

            _logger.LogInformation(
                "Validated {TotalRows} results for batch {BatchId}",
                request.Results.Count, request.UploadBatchId);

            return Ok(BuildResponse(request.UploadBatchId, request.Results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating results");
            return BadRequest($"Error validating results: {ex.Message}");
        }
    }

    /// <summary>Save validated results to the database.</summary>
    [HttpPost("save")]
    [ProducesResponseType(typeof(SaveResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaveResultsResponse>> SaveResults([FromBody] SaveResultsRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var uploadBatch = await _context.UploadBatches.FindAsync(request.UploadBatchId);
            if (uploadBatch == null)
                return NotFound($"Upload batch {request.UploadBatchId} not found");

            var race = await _context.Races.FindAsync(request.RaceId);
            if (race == null)
                return NotFound($"Race {request.RaceId} not found");

            var user       = await _userManager.GetUserAsync(User);
            var resultIds  = new List<Guid>();
            var newRunners = 0;

            foreach (var resultRow in request.Results)
            {
                Guid runnerId;

                if (resultRow.MatchedRunnerId.HasValue)
                {
                    runnerId = resultRow.MatchedRunnerId.Value;
                }
                else
                {
                    if (!resultRow.Gender.HasValue)
                    {
                        _logger.LogWarning("Skipping result with missing gender: {Name}", resultRow.Name);
                        continue;
                    }

                    var newRunner = new Runner
                    {
                        RunnerId    = Guid.NewGuid(),
                        FirstName   = resultRow.Name.Split(' ').FirstOrDefault() ?? resultRow.Name,
                        LastName    = string.Join(" ", resultRow.Name.Split(' ').Skip(1)),
                        Gender      = resultRow.Gender.Value,
                        DateOfBirth = resultRow.Age.HasValue
                            ? DateTime.UtcNow.AddYears(-resultRow.Age.Value)
                            : null,
                        CreatedAt   = DateTime.UtcNow,
                        UpdatedAt   = DateTime.UtcNow
                    };
                    _context.Runners.Add(newRunner);
                    runnerId = newRunner.RunnerId;
                    newRunners++;
                }

                if (!resultRow.Gender.HasValue)
                {
                    _logger.LogWarning("Skipping result with missing gender: {Name}", resultRow.Name);
                    continue;
                }

                var raceResult = new RaceResult
                {
                    ResultId     = Guid.NewGuid(),
                    RaceId       = request.RaceId,
                    RunnerId     = runnerId,
                    Bib          = resultRow.Bib,
                    Place        = resultRow.Place,
                    Time         = resultRow.Time,
                    Age          = resultRow.Age ?? 0,
                    Gender       = resultRow.Gender.Value,
                    Status       = resultRow.Status,
                    Notes        = resultRow.Notes,
                    CreatedAt    = DateTime.UtcNow,
                    UploadedBy   = user?.Id ?? "system",
                    UploadBatchId = request.UploadBatchId
                };
                _context.RaceResults.Add(raceResult);
                resultIds.Add(raceResult.ResultId);
            }

            await _context.SaveChangesAsync();
            await CalculateGenderPlacesAsync(request.RaceId);

            uploadBatch.Status = UploadStatus.Saved;
            await _context.SaveChangesAsync();

            if (race.IsGrandPrixRace)
            {
                _logger.LogInformation("Calculating Grand Prix points for race {RaceId}", request.RaceId);
                await _grandPrixCalculationService.CalculateRacePointsAsync(request.RaceId);
                await _grandPrixCalculationService.UpdateStandingsAsync(race.Year);
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Saved {ResultCount} results for race {RaceId}. Created {NewRunners} new runners",
                resultIds.Count, request.RaceId, newRunners);

            return Ok(new SaveResultsResponse
            {
                Success          = true,
                ResultsSaved     = resultIds.Count,
                NewRunnersCreated = newRunners,
                ResultIds        = resultIds,
                Message          = $"Successfully saved {resultIds.Count} results" +
                                   (race.IsGrandPrixRace ? " and calculated Grand Prix points" : "")
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error saving results");
            return BadRequest($"Error saving results: {ex.Message}");
        }
    }

    /// <summary>Get all results for a specific race.</summary>
    [HttpGet("race/{raceId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RaceResultDto>>> GetRaceResults(Guid raceId)
    {
        var race = await _context.Races.FindAsync(raceId);
        if (race == null)
            return NotFound($"Race {raceId} not found");

        var results = await _context.RaceResults
            .Include(r => r.Runner)
            .Include(r => r.Race)
            .Where(r => r.RaceId == raceId)
            .OrderBy(r => r.Place)
            .ThenBy(r => r.Time)
            .Select(r => new RaceResultDto
            {
                ResultId         = r.ResultId,
                RaceId           = r.RaceId,
                RaceName         = r.Race.Name,
                RaceDate         = r.Race.Date,
                RunnerId         = r.RunnerId,
                RunnerName       = r.Runner.FirstName + " " + r.Runner.LastName,
                Bib              = r.Bib,
                Place            = r.Place,
                PlaceGender      = r.PlaceGender,
                PlaceAgeCategory = r.PlaceAgeCategory,
                Time             = r.Time,
                Age              = r.Age,
                Gender           = r.Gender,
                Status           = r.Status,
                Notes            = r.Notes,
                IsNewRecord      = r.IsNewRecord,
                CreatedAt        = r.CreatedAt
            })
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>List upload batches, optionally filtered by year.</summary>
    [HttpGet("batches")]
    [ProducesResponseType(typeof(List<UploadBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UploadBatchDto>>> GetBatches([FromQuery] int? year)
    {
        var query = _context.UploadBatches.Include(b => b.Race).AsQueryable();
        if (year.HasValue)
            query = query.Where(b => b.Race.Year == year.Value);

        var batches = await query
            .OrderByDescending(b => b.UploadedAt)
            .Select(b => new UploadBatchDto
            {
                UploadBatchId   = b.UploadBatchId,
                RaceId          = b.RaceId,
                RaceName        = b.Race.Name,
                RaceDate        = b.Race.Date,
                IsGrandPrixRace = b.Race.IsGrandPrixRace,
                FileName        = b.FileName,
                FileType        = b.FileType,
                RecordsUploaded = b.RecordsUploaded,
                UploadedBy      = b.UploadedBy,
                UploadedAt      = b.UploadedAt,
                Status          = b.Status
            })
            .ToListAsync();

        return Ok(batches);
    }

    /// <summary>Delete an upload batch and all associated results.</summary>
    [HttpDelete("batch/{batchId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUploadBatch(Guid batchId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var uploadBatch = await _context.UploadBatches
                .Include(b => b.Race)
                .FirstOrDefaultAsync(b => b.UploadBatchId == batchId);

            if (uploadBatch == null)
                return NotFound($"Upload batch {batchId} not found");

            var results = await _context.RaceResults
                .Where(r => r.UploadBatchId == batchId)
                .ToListAsync();
            _context.RaceResults.RemoveRange(results);

            var resultIds = results.Select(r => r.ResultId).ToList();
            var points    = await _context.GrandPrixPoints
                .Where(p => resultIds.Contains(p.ResultId))
                .ToListAsync();
            _context.GrandPrixPoints.RemoveRange(points);
            _context.UploadBatches.Remove(uploadBatch);

            await _context.SaveChangesAsync();

            if (uploadBatch.Race.IsGrandPrixRace)
                await _grandPrixCalculationService.UpdateStandingsAsync(uploadBatch.Race.Year);

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Deleted upload batch {BatchId} with {ResultCount} results", batchId, results.Count);

            return NoContent();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting upload batch {BatchId}", batchId);
            return BadRequest($"Error deleting upload batch: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task MatchRunnersAsync(List<ResultRow> results)
    {
        foreach (var result in results)
        {
            if (!string.IsNullOrEmpty(result.Name))
            {
                var matches = await _runnerMatchingService.FindMatchesAsync(
                    result.Name, result.Age, result.Gender);
                result.RunnerMatches = matches.Select(m => RunnerMatchDto.FromRunnerMatch(m)).ToList();
            }
        }
    }

    private static UploadResultsResponse BuildResponse(Guid batchId, List<ResultRow> results) =>
        new()
        {
            UploadBatchId  = batchId,
            ParsedResults  = results,
            TotalRows      = results.Count,
            ValidRows      = results.Count(r => r.ValidationIssues.Count == 0),
            RowsWithIssues = results.Count(r => r.ValidationIssues.Count > 0)
        };

    private async Task CalculateGenderPlacesAsync(Guid raceId)
    {
        var results = await _context.RaceResults
            .Where(r => r.RaceId == raceId && r.Status == ResultStatus.Finished)
            .OrderBy(r => r.Place)
            .ToListAsync();

        var males   = results.Where(r => r.Gender == Gender.Male).ToList();
        var females = results.Where(r => r.Gender == Gender.Female).ToList();

        for (int i = 0; i < males.Count;   i++) males[i].PlaceGender   = i + 1;
        for (int i = 0; i < females.Count; i++) females[i].PlaceGender = i + 1;

        await _context.SaveChangesAsync();
    }
}
