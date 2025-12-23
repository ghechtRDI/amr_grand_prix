using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using AmrGrandPrix.API.Models.DTOs.RaceResults;
using AmrGrandPrix.API.Services.FileParser;
using AmrGrandPrix.API.Services.ResultsProcessing;
using AmrGrandPrix.API.Services.GrandPrix;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// Controller for managing race results uploads and processing
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class ResultsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly FileParserFactory _fileParserFactory;
    private readonly IResultsProcessingService _resultsProcessingService;
    private readonly IRunnerMatchingService _runnerMatchingService;
    private readonly IGrandPrixCalculationService _grandPrixCalculationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ResultsController> _logger;

    public ResultsController(
        ApplicationDbContext context,
        FileParserFactory fileParserFactory,
        IResultsProcessingService resultsProcessingService,
        IRunnerMatchingService runnerMatchingService,
        IGrandPrixCalculationService grandPrixCalculationService,
        UserManager<ApplicationUser> userManager,
        ILogger<ResultsController> logger)
    {
        _context = context;
        _fileParserFactory = fileParserFactory;
        _resultsProcessingService = resultsProcessingService;
        _runnerMatchingService = runnerMatchingService;
        _grandPrixCalculationService = grandPrixCalculationService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Upload and parse race results file
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadResultsResponse>> UploadResults([FromForm] UploadResultsRequest request)
    {
        try
        {
            // Validate race exists
            var race = await _context.Races.FindAsync(request.RaceId);
            if (race == null)
            {
                return NotFound($"Race with ID {request.RaceId} not found");
            }

            // Validate file
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Validate file size (max 10MB)
            if (request.File.Length > 10 * 1024 * 1024)
            {
                return BadRequest("File size exceeds 10MB limit");
            }

            // Parse file based on extension
            var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            var parser = _fileParserFactory.GetParser(fileExtension);

            List<RawResultRow> rawResults;
            using (var stream = request.File.OpenReadStream())
            {
                rawResults = await parser.ParseAsync(stream, request.File.FileName);
            }

            // Process results
            var processedResults = await _resultsProcessingService.ProcessResultsAsync(rawResults);

            // Match runners for each result
            foreach (var result in processedResults)
            {
                if (!string.IsNullOrEmpty(result.Name))
                {
                    var matches = await _runnerMatchingService.FindMatchesAsync(
                        result.Name,
                        result.Age,
                        result.Gender);

                    result.RunnerMatches = matches.Select(m => RunnerMatchDto.FromRunnerMatch(m)).ToList();
                }
            }

            // Create upload batch record
            var user = await _userManager.GetUserAsync(User);
            var uploadBatch = new UploadBatch
            {
                UploadBatchId = Guid.NewGuid(),
                RaceId = request.RaceId,
                FileName = request.File.FileName,
                FileType = fileExtension switch
                {
                    ".csv" => FileType.CSV,
                    ".xlsx" or ".xls" => FileType.Excel,
                    ".pdf" => FileType.PDF,
                    _ => FileType.CSV
                },
                RecordsUploaded = processedResults.Count,
                UploadedBy = user?.Id ?? "system",
                UploadedAt = DateTime.UtcNow,
                Status = UploadStatus.Pending
            };

            _context.UploadBatches.Add(uploadBatch);
            await _context.SaveChangesAsync();

            // Build response
            var response = new UploadResultsResponse
            {
                UploadBatchId = uploadBatch.UploadBatchId,
                ParsedResults = processedResults,
                DetectedColumns = rawResults.FirstOrDefault()?.Columns.Keys.ToList() ?? new List<string>(),
                TotalRows = processedResults.Count,
                ValidRows = processedResults.Count(r => r.ValidationIssues.Count == 0),
                RowsWithIssues = processedResults.Count(r => r.ValidationIssues.Count > 0)
            };

            _logger.LogInformation(
                "File {FileName} uploaded for race {RaceId} by {UserId}. Parsed {TotalRows} rows with {Issues} issues",
                request.File.FileName, request.RaceId, user?.Id, response.TotalRows, response.RowsWithIssues);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading results file");
            return BadRequest($"Error processing file: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate corrected results data
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(UploadResultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadResultsResponse>> ValidateResults([FromBody] ValidateResultsRequest request)
    {
        try
        {
            var uploadBatch = await _context.UploadBatches.FindAsync(request.UploadBatchId);
            if (uploadBatch == null)
            {
                return NotFound($"Upload batch {request.UploadBatchId} not found");
            }

            // Re-validate all results after user corrections
            foreach (var result in request.Results)
            {
                result.ValidationIssues = _resultsProcessingService.ValidateRow(result);
            }

            var response = new UploadResultsResponse
            {
                UploadBatchId = request.UploadBatchId,
                ParsedResults = request.Results,
                TotalRows = request.Results.Count,
                ValidRows = request.Results.Count(r => r.ValidationIssues.Count == 0),
                RowsWithIssues = request.Results.Count(r => r.ValidationIssues.Count > 0)
            };

            _logger.LogInformation(
                "Validated {TotalRows} results for batch {UploadBatchId}. {ValidRows} valid, {Issues} with issues",
                response.TotalRows, request.UploadBatchId, response.ValidRows, response.RowsWithIssues);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating results");
            return BadRequest($"Error validating results: {ex.Message}");
        }
    }

    /// <summary>
    /// Save validated results to database
    /// </summary>
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
            {
                return NotFound($"Upload batch {request.UploadBatchId} not found");
            }

            var race = await _context.Races.FindAsync(request.RaceId);
            if (race == null)
            {
                return NotFound($"Race {request.RaceId} not found");
            }

            var user = await _userManager.GetUserAsync(User);
            var resultIds = new List<Guid>();
            var newRunnersCreated = 0;

            foreach (var resultRow in request.Results)
            {
                // Find or create runner
                Guid runnerId;

                if (resultRow.MatchedRunnerId.HasValue)
                {
                    // Use matched runner
                    runnerId = resultRow.MatchedRunnerId.Value;
                }
                else
                {
                    // Skip if gender is not specified - validation should have caught this
                    if (!resultRow.Gender.HasValue)
                    {
                        _logger.LogWarning("Skipping result with missing gender: {Name}", resultRow.Name);
                        continue;
                    }

                    // Create new runner
                    var newRunner = new Runner
                    {
                        RunnerId = Guid.NewGuid(),
                        FirstName = resultRow.Name.Split(' ').FirstOrDefault() ?? resultRow.Name,
                        LastName = string.Join(" ", resultRow.Name.Split(' ').Skip(1)),
                        Gender = resultRow.Gender.Value,
                        DateOfBirth = resultRow.Age.HasValue
                            ? DateTime.UtcNow.AddYears(-resultRow.Age.Value)
                            : null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Runners.Add(newRunner);
                    runnerId = newRunner.RunnerId;
                    newRunnersCreated++;
                }

                // Create race result (gender should be present at this point)
                if (!resultRow.Gender.HasValue)
                {
                    _logger.LogWarning("Skipping result with missing gender: {Name}", resultRow.Name);
                    continue;
                }

                var raceResult = new RaceResult
                {
                    ResultId = Guid.NewGuid(),
                    RaceId = request.RaceId,
                    RunnerId = runnerId,
                    Bib = resultRow.Bib,
                    Place = resultRow.Place,
                    Time = resultRow.Time,
                    Age = resultRow.Age ?? 0,
                    Gender = resultRow.Gender.Value,
                    Status = resultRow.Status,
                    Notes = resultRow.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UploadedBy = user?.Id ?? "system",
                    UploadBatchId = request.UploadBatchId
                };

                _context.RaceResults.Add(raceResult);
                resultIds.Add(raceResult.ResultId);
            }

            // Save all results
            await _context.SaveChangesAsync();

            // Calculate gender-specific places
            await CalculateGenderPlacesAsync(request.RaceId);

            // Update upload batch status
            uploadBatch.Status = UploadStatus.Saved;
            await _context.SaveChangesAsync();

            // If this is a Grand Prix race, calculate points
            if (race.IsGrandPrixRace)
            {
                _logger.LogInformation("Calculating Grand Prix points for race {RaceId}", request.RaceId);
                await _grandPrixCalculationService.CalculateRacePointsAsync(request.RaceId);
                await _grandPrixCalculationService.UpdateStandingsAsync(race.Year);
            }

            await transaction.CommitAsync();

            var response = new SaveResultsResponse
            {
                Success = true,
                ResultsSaved = resultIds.Count,
                NewRunnersCreated = newRunnersCreated,
                ResultIds = resultIds,
                Message = $"Successfully saved {resultIds.Count} results" +
                          (race.IsGrandPrixRace ? " and calculated Grand Prix points" : "")
            };

            _logger.LogInformation(
                "Saved {ResultCount} results for race {RaceId}. Created {NewRunners} new runners",
                resultIds.Count, request.RaceId, newRunnersCreated);

            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error saving results");
            return BadRequest($"Error saving results: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all results for a specific race
    /// </summary>
    [HttpGet("race/{raceId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<RaceResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RaceResult>>> GetRaceResults(Guid raceId)
    {
        var race = await _context.Races.FindAsync(raceId);
        if (race == null)
        {
            return NotFound($"Race {raceId} not found");
        }

        var results = await _context.RaceResults
            .Include(r => r.Runner)
            .Where(r => r.RaceId == raceId)
            .OrderBy(r => r.Place)
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>
    /// Delete an upload batch and all associated results
    /// </summary>
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
            {
                return NotFound($"Upload batch {batchId} not found");
            }

            // Delete associated results
            var results = await _context.RaceResults
                .Where(r => r.UploadBatchId == batchId)
                .ToListAsync();

            _context.RaceResults.RemoveRange(results);

            // Delete associated Grand Prix points
            var resultIds = results.Select(r => r.ResultId).ToList();
            var points = await _context.GrandPrixPoints
                .Where(p => resultIds.Contains(p.ResultId))
                .ToListAsync();

            _context.GrandPrixPoints.RemoveRange(points);

            // Delete upload batch
            _context.UploadBatches.Remove(uploadBatch);

            await _context.SaveChangesAsync();

            // Recalculate standings if GP race
            if (uploadBatch.Race.IsGrandPrixRace)
            {
                await _grandPrixCalculationService.UpdateStandingsAsync(uploadBatch.Race.Year);
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Deleted upload batch {BatchId} with {ResultCount} results",
                batchId, results.Count);

            return NoContent();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting upload batch {BatchId}", batchId);
            return BadRequest($"Error deleting upload batch: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate gender-specific places for a race
    /// </summary>
    private async Task CalculateGenderPlacesAsync(Guid raceId)
    {
        var results = await _context.RaceResults
            .Where(r => r.RaceId == raceId && r.Status == ResultStatus.Finished)
            .OrderBy(r => r.Place)
            .ToListAsync();

        // Calculate male places
        var maleResults = results.Where(r => r.Gender == Gender.Male).ToList();
        for (int i = 0; i < maleResults.Count; i++)
        {
            maleResults[i].PlaceGender = i + 1;
        }

        // Calculate female places
        var femaleResults = results.Where(r => r.Gender == Gender.Female).ToList();
        for (int i = 0; i < femaleResults.Count; i++)
        {
            femaleResults[i].PlaceGender = i + 1;
        }

        await _context.SaveChangesAsync();
    }
}
