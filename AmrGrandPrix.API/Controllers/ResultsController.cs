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
    private readonly ExtractionJobs _extractionJobs;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ResultsController> _logger;

    public ResultsController(
        ApplicationDbContext context,
        ILlmExtractionService llmExtractionService,
        IResultsProcessingService resultsProcessingService,
        IRunnerMatchingService runnerMatchingService,
        IGrandPrixCalculationService grandPrixCalculationService,
        ExtractionJobs extractionJobs,
        UserManager<ApplicationUser> userManager,
        ILogger<ResultsController> logger)
    {
        _context                  = context;
        _llmExtractionService     = llmExtractionService;
        _resultsProcessingService = resultsProcessingService;
        _runnerMatchingService    = runnerMatchingService;
        _grandPrixCalculationService = grandPrixCalculationService;
        _extractionJobs           = extractionJobs;
        _userManager              = userManager;
        _logger                   = logger;
    }

    /// <summary>
    /// Upload a race results file (PDF, CSV, or Excel). The file's text is read here, but the LLM
    /// extraction runs in the background (it can take minutes - longer than Cloudflare holds a
    /// request open): this returns 202 with the batch ID; poll <see cref="GetUploadStatus"/>.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadAcceptedResponse>> UploadResults(
        [FromForm] UploadResultsRequest request,
        CancellationToken ct)
    {
        try
        {
            // A normal upload targets one race. A multi-variant upload instead targets the series +
            // date and the variants the admin says the file contains: their races are created when
            // the reviewed results are saved, and only for variants that actually had results.
            Race? race = null;
            ICollection<RaceVariant> seriesVariants;
            var includedVariants = new List<RaceVariant>();
            DateOnly raceDate;
            if (request.RaceId.HasValue)
            {
                race = await _context.Races
                    .Include(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
                    .FirstOrDefaultAsync(r => r.RaceId == request.RaceId, ct);
                if (race == null)
                    return NotFound($"Race with ID {request.RaceId} not found");
                seriesVariants = race.RaceVariant.RaceSeries.Variants;
                raceDate = race.Date;
            }
            else if (request.IncludedVariantIds.Count > 0 && request.RaceSeriesId.HasValue && request.RaceDate.HasValue)
            {
                var series = await _context.RaceSeries
                    .Include(s => s.Variants)
                    .FirstOrDefaultAsync(s => s.RaceSeriesId == request.RaceSeriesId, ct);
                if (series == null)
                    return NotFound($"Race series with ID {request.RaceSeriesId} not found");
                seriesVariants = series.Variants;
                includedVariants = series.Variants
                    .Where(v => request.IncludedVariantIds.Contains(v.RaceVariantId))
                    .OrderBy(v => v.DisplayOrder).ThenBy(v => v.Name)
                    .ToList();
                if (includedVariants.Count != request.IncludedVariantIds.Distinct().Count())
                    return BadRequest("Every included variant must belong to the race series");
                raceDate = request.RaceDate.Value;
            }
            else
            {
                return BadRequest("RaceId is required (or RaceSeriesId, RaceDate and IncludedVariantIds for a multi-variant upload)");
            }

            if (request.File == null || request.File.Length == 0)
                return BadRequest("No file uploaded");

            if (request.File.Length > 10 * 1024 * 1024)
                return BadRequest("File size exceeds 10MB limit");

            var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();

            // A multi-variant upload tells the LLM exactly which variants the file contains.
            // Otherwise hint it with the series' canonical variant names (plus any extra typed in
            // the wizard) so a multi-variant file gets labeled consistently.
            var onlyTheseVariants = includedVariants.Count > 0;
            List<string> knownVariants;
            if (onlyTheseVariants)
            {
                knownVariants = includedVariants.Select(v => v.Name).ToList();
            }
            else
            {
                knownVariants = (seriesVariants.Count > 1
                        ? seriesVariants.OrderBy(v => v.DisplayOrder).Select(v => v.Name)
                        : Enumerable.Empty<string>())
                    .Concat((request.KnownVariants ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            string text;
            using (var stream = request.File.OpenReadStream())
                text = await _llmExtractionService.ExtractTextAsync(stream, request.File.FileName);
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest("No text could be read from this file. If it's a scanned PDF, export the results as CSV or Excel instead.");

            var user = await _userManager.GetUserAsync(User);
            var uploadBatch = new UploadBatch
            {
                UploadBatchId    = Guid.NewGuid(),
                RaceId           = race?.RaceId,
                RaceSeriesId     = race == null ? request.RaceSeriesId : null,
                RaceDate         = race == null ? raceDate : null,
                IncludedVariantIds = includedVariants.Select(v => v.RaceVariantId).ToList(),
                FileName         = request.File.FileName,
                FileType         = ext switch
                {
                    ".csv" or ".txt" => FileType.CSV,
                    ".xlsx" or ".xls" => FileType.Excel,
                    ".pdf"           => FileType.PDF,
                    _                => FileType.CSV
                },
                UploadedBy        = user?.Id ?? "system",
                UploadedAt        = DateTime.UtcNow,
                Status            = UploadStatus.Extracting,
                ExtractedText     = text,
                KnownVariants     = knownVariants,
                OnlyTheseVariants = onlyTheseVariants,
                ExpectedRows      = ResultRowEstimator.Estimate(text)
            };

            _context.UploadBatches.Add(uploadBatch);
            await _context.SaveChangesAsync(ct);
            _extractionJobs.Enqueue(uploadBatch.UploadBatchId);

            _logger.LogInformation(
                "File {FileName} uploaded for race {RaceId} (series {RaceSeriesId}) by {UserId}; queued for extraction as batch {BatchId} (~{ExpectedRows} rows)",
                request.File.FileName, race?.RaceId, request.RaceSeriesId, user?.Id,
                uploadBatch.UploadBatchId, uploadBatch.ExpectedRows);

            return AcceptedAtAction(nameof(GetUploadStatus), new { batchId = uploadBatch.UploadBatchId },
                new UploadAcceptedResponse
                {
                    UploadBatchId = uploadBatch.UploadBatchId,
                    ExpectedRows  = uploadBatch.ExpectedRows
                });
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
    /// <summary>
    /// Progress of an upload's background extraction. Once extraction is done this returns the
    /// parsed, matched results (the same shape the upload itself used to return).
    /// </summary>
    [HttpGet("upload/{batchId:guid}")]
    [ProducesResponseType(typeof(UploadStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadStatusResponse>> GetUploadStatus(Guid batchId)
    {
        // Cheap lookup first - this is polled every couple of seconds while extracting.
        var state = await _context.UploadBatches
            .Where(b => b.UploadBatchId == batchId)
            .Select(b => new { b.Status, b.ExpectedRows, b.ExtractionError })
            .FirstOrDefaultAsync();
        if (state == null)
            return NotFound($"Upload batch {batchId} not found");

        var response = new UploadStatusResponse { UploadBatchId = batchId, ExpectedRows = state.ExpectedRows };
        switch (state.Status)
        {
            case UploadStatus.Extracting:
                // Not in the queue yet = the app just restarted and the worker is re-queuing it.
                var progress = _extractionJobs.GetProgress(batchId);
                response.State = progress?.Started == true ? ExtractionState.Extracting : ExtractionState.Queued;
                response.RowsExtracted = progress?.RowsExtracted ?? 0;
                return Ok(response);

            case UploadStatus.Failed:
                response.State = ExtractionState.Failed;
                response.Error = state.ExtractionError;
                return Ok(response);

            case UploadStatus.Pending:
                try
                {
                    var batch = (await LoadBatchWithSeriesAsync(batchId))!;
                    if (ResolveSeriesAndDate(batch) is not var (series, raceDate))
                        return BadRequest("This upload batch isn't linked to a race or race series");

                    var results = await ProcessStoredExtractionAsync(batch, RoutingVariants(batch, series), raceDate);
                    response.State = ExtractionState.Ready;
                    response.RowsExtracted = results.Count;
                    response.Result = BuildResponse(batchId, results);
                    return Ok(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing extracted results for batch {BatchId}", batchId);
                    return BadRequest($"Error processing extracted results: {ex.Message}");
                }

            default:
                return BadRequest($"Upload batch is already {state.Status}");
        }
    }

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
            var race = await _context.Races.FindAsync(request.RaceId);
            if (race == null)
                return NotFound($"Race {request.RaceId} not found");
            if (await RejectIfSeasonFinalizedAsync(race) is { } finalized)
                return finalized;

            var user = await _userManager.GetUserAsync(User);

            UploadBatch? uploadBatch;
            if (request.UploadBatchId.HasValue)
            {
                uploadBatch = await _context.UploadBatches.FindAsync(request.UploadBatchId.Value);
                if (uploadBatch == null)
                    return NotFound($"Upload batch {request.UploadBatchId} not found");

                // The batch was created against the race picked in Step 1, but its rows may have
                // been routed to another variant's race during review.
                uploadBatch.RaceId = request.RaceId;
                uploadBatch.RaceSeriesId = null;
                uploadBatch.RaceDate = null;
            }
            else if (request.SourceUploadBatchId.HasValue)
            {
                // An additional course-variant group split out of the same upload into a
                // different race — clone a new batch from the source for audit lineage.
                var sourceBatch = await _context.UploadBatches.FindAsync(request.SourceUploadBatchId.Value);
                if (sourceBatch == null)
                    return NotFound($"Upload batch {request.SourceUploadBatchId} not found");

                uploadBatch = new UploadBatch
                {
                    UploadBatchId   = Guid.NewGuid(),
                    RaceId          = request.RaceId,
                    FileName        = sourceBatch.FileName,
                    FileType        = sourceBatch.FileType,
                    RecordsUploaded = request.Results.Count,
                    UploadedBy      = user?.Id ?? "system",
                    UploadedAt      = DateTime.UtcNow,
                    Status          = UploadStatus.Saved,
                    RawLlmJson      = sourceBatch.RawLlmJson,
                    LlmModel        = sourceBatch.LlmModel,
                    LlmInputTokens  = sourceBatch.LlmInputTokens,
                    LlmOutputTokens = sourceBatch.LlmOutputTokens
                };
                _context.UploadBatches.Add(uploadBatch);
            }
            else
            {
                return BadRequest("Either UploadBatchId or SourceUploadBatchId is required");
            }

            var resultIds  = new List<Guid>();
            var newRunners = 0;
            var skipped    = new List<SkippedResultDto>();

            foreach (var resultRow in request.Results)
            {
                if (!resultRow.Gender.HasValue)
                {
                    _logger.LogWarning("Skipping result with missing gender: {Name}", resultRow.Name);
                    skipped.Add(new SkippedResultDto
                    {
                        RowNumber = resultRow.RowNumber,
                        Name      = resultRow.Name,
                        Reason    = "Missing gender"
                    });
                    continue;
                }

                Guid runnerId;

                if (resultRow.MatchedRunnerId.HasValue)
                {
                    runnerId = resultRow.MatchedRunnerId.Value;

                    // Admin opted to correct the stored age (e.g. it was estimated from a
                    // previous upload that only reported an age category). Never overwrite a
                    // runner's verified, self-reported date of birth this way.
                    if (resultRow.UpdateRunnerAge && resultRow.Age.HasValue)
                    {
                        var matchedRunner = await _context.Runners.FindAsync(runnerId);
                        if (matchedRunner != null && !matchedRunner.DateOfBirth.HasValue)
                        {
                            matchedRunner.EstimatedBirthYear = race.Date.Year - resultRow.Age.Value;
                            matchedRunner.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                else
                {
                    var newRunner = new Runner
                    {
                        RunnerId    = Guid.NewGuid(),
                        FirstName   = resultRow.Name.Split(' ').FirstOrDefault() ?? resultRow.Name,
                        LastName    = string.Join(" ", resultRow.Name.Split(' ').Skip(1)),
                        Gender      = resultRow.Gender.Value,
                        EstimatedBirthYear = resultRow.Age.HasValue
                            ? race.Date.Year - resultRow.Age.Value
                            : null,
                        CreatedAt   = DateTime.UtcNow,
                        UpdatedAt   = DateTime.UtcNow
                    };
                    _context.Runners.Add(newRunner);
                    runnerId = newRunner.RunnerId;
                    newRunners++;
                }

                var raceResult = new RaceResult
                {
                    ResultId     = Guid.NewGuid(),
                    RaceId       = request.RaceId,
                    RunnerId     = runnerId,
                    Bib          = resultRow.Bib,
                    Place        = resultRow.Place,
                    Time         = _resultsProcessingService.ParseTime(resultRow.TimeString),
                    Age          = resultRow.Age,
                    AgeCategory  = resultRow.AgeCategory
                        ?? (resultRow.Age.HasValue ? GrandPrixConstants.GetAgeCategory(resultRow.Age.Value) : null),
                    Gender       = resultRow.Gender.Value,
                    Status       = resultRow.Status,
                    Notes        = resultRow.Notes,
                    CreatedAt    = DateTime.UtcNow,
                    UploadedBy   = user?.Id ?? "system",
                    UploadBatchId = uploadBatch.UploadBatchId
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
                "Saved {ResultCount} results for race {RaceId}. Created {NewRunners} new runners. Skipped {SkippedCount}",
                resultIds.Count, request.RaceId, newRunners, skipped.Count);

            return Ok(new SaveResultsResponse
            {
                Success          = true,
                RaceId           = request.RaceId,
                ResultsSaved     = resultIds.Count,
                NewRunnersCreated = newRunners,
                ResultIds        = resultIds,
                SkippedResults   = skipped,
                Message          = $"Successfully saved {resultIds.Count} results" +
                                   (race.IsGrandPrixRace ? " and calculated Grand Prix points" : "") +
                                   (skipped.Count > 0 ? $"; skipped {skipped.Count} result(s) — see details" : "")
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
            .OrderBy(r => r.Status != ResultStatus.Finished
                          || r.Place == null || r.Place == 0
                          || r.Time == null || r.Time == TimeSpan.Zero)
            .ThenBy(r => r.Place)
            .ThenBy(r => r.Time)
            .Select(r => new RaceResultDto
            {
                ResultId         = r.ResultId,
                RaceId           = r.RaceId,
                RaceName         = RaceProjections.DisplayName(r.Race.RaceVariant.RaceSeries.Name, r.Race.RaceVariant.Name, r.Race.RaceVariant.RaceSeries.Variants.Count),
                RaceDate         = r.Race.Date,
                RunnerId         = r.RunnerId,
                RunnerName       = r.Runner.FirstName + " " + r.Runner.LastName,
                Bib              = r.Bib,
                Place            = r.Place,
                PlaceGender      = r.PlaceGender,
                PlaceAgeCategory = r.PlaceAgeCategory,
                Time             = r.Time,
                Age              = r.Age,
                AgeCategory      = r.AgeCategory,
                Gender           = r.Gender,
                Status           = r.Status,
                Notes            = r.Notes,
                IsNewRecord      = r.IsNewRecord,
                CreatedAt        = r.CreatedAt
            })
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>Update an individual race result (admin/manager only).</summary>
    [HttpPut("{resultId}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(RaceResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RaceResultDto>> UpdateRaceResult(Guid resultId, [FromBody] UpdateRaceResultRequest request)
    {
        try
        {
            var result = await _context.RaceResults
                .Include(r => r.Runner)
                .Include(r => r.Race).ThenInclude(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
                .FirstOrDefaultAsync(r => r.ResultId == resultId);

            if (result == null)
                return NotFound($"Result {resultId} not found");
            if (await RejectIfSeasonFinalizedAsync(result.Race) is { } finalized)
                return finalized;

            result.Bib = request.Bib;
            result.Place = request.Place;
            result.Time = _resultsProcessingService.ParseTime(request.TimeString);
            result.Age = request.Age;
            result.AgeCategory = request.AgeCategory
                ?? (request.Age.HasValue ? GrandPrixConstants.GetAgeCategory(request.Age.Value) : null);
            result.Gender = request.Gender;
            result.Status = request.Status;
            result.Notes = request.Notes;

            await _context.SaveChangesAsync();
            await CalculateGenderPlacesAsync(result.RaceId);
            await _grandPrixCalculationService.RecalculateAfterResultsChangeAsync(result.RaceId);

            _logger.LogInformation("Updated result {ResultId} for race {RaceId}", resultId, result.RaceId);

            return Ok(new RaceResultDto
            {
                ResultId         = result.ResultId,
                RaceId           = result.RaceId,
                RaceName         = RaceProjections.DisplayName(result.Race),
                RaceDate         = result.Race.Date,
                RunnerId         = result.RunnerId,
                RunnerName       = result.Runner.FirstName + " " + result.Runner.LastName,
                Bib              = result.Bib,
                Place            = result.Place,
                PlaceGender      = result.PlaceGender,
                PlaceAgeCategory = result.PlaceAgeCategory,
                Time             = result.Time,
                Age              = result.Age,
                AgeCategory      = result.AgeCategory,
                Gender           = result.Gender,
                Status           = result.Status,
                Notes            = result.Notes,
                IsNewRecord      = result.IsNewRecord,
                CreatedAt        = result.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating result {ResultId}", resultId);
            return BadRequest($"Error updating result: {ex.Message}");
        }
    }

    /// <summary>Delete an individual race result (admin only).</summary>
    [HttpDelete("{resultId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRaceResult(Guid resultId)
    {
        try
        {
            var result = await _context.RaceResults.Include(r => r.Race).FirstOrDefaultAsync(r => r.ResultId == resultId);
            if (result == null)
                return NotFound($"Result {resultId} not found");
            if (await RejectIfSeasonFinalizedAsync(result.Race) is { } finalized)
                return finalized;

            var raceId = result.RaceId;

            var points = await _context.GrandPrixPoints
                .Where(p => p.ResultId == resultId)
                .ToListAsync();
            _context.GrandPrixPoints.RemoveRange(points);
            _context.RaceResults.Remove(result);

            await _context.SaveChangesAsync();
            await CalculateGenderPlacesAsync(raceId);
            await _grandPrixCalculationService.RecalculateAfterResultsChangeAsync(raceId);

            _logger.LogInformation("Deleted result {ResultId} from race {RaceId}", resultId, raceId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting result {ResultId}", resultId);
            return BadRequest($"Error deleting result: {ex.Message}");
        }
    }

    /// <summary>Resume a pending upload batch, re-deriving its parsed results without re-running the LLM.</summary>
    [HttpGet("batch/{batchId}/resume")]
    [ProducesResponseType(typeof(ResumeBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResumeBatchResponse>> ResumeBatch(Guid batchId)
    {
        var uploadBatch = await LoadBatchWithSeriesAsync(batchId);

        if (uploadBatch == null)
            return NotFound($"Upload batch {batchId} not found");

        if (uploadBatch.Status != UploadStatus.Pending)
            return BadRequest($"Only pending uploads can be resumed (batch is {uploadBatch.Status})");

        if (string.IsNullOrEmpty(uploadBatch.RawLlmJson))
            return BadRequest("This upload batch has no stored extraction data to resume from");

        try
        {
            var race = uploadBatch.Race;
            if (ResolveSeriesAndDate(uploadBatch) is not var (series, raceDate))
                return BadRequest("This upload batch isn't linked to a race or race series");

            var includedVariants = RoutingVariants(uploadBatch, series);
            var processedResults = await ProcessStoredExtractionAsync(uploadBatch, includedVariants, raceDate);

            return Ok(new ResumeBatchResponse
            {
                UploadBatchId   = uploadBatch.UploadBatchId,
                RaceId          = race?.RaceId,
                RaceName        = series.Name,
                RaceDate        = raceDate,
                IsGrandPrixRace = race?.IsGrandPrixRace ?? false,
                RaceSeriesId    = series.RaceSeriesId,
                RaceVariantId   = race?.RaceVariantId,
                CourseVariant   = race != null && series.Variants.Count > 1 ? race.RaceVariant.Name : null,
                IncludedVariantIds = race == null
                    ? includedVariants.OrderBy(v => v.DisplayOrder).Select(v => v.RaceVariantId).ToList()
                    : new List<Guid>(),
                IncludedVariantNames = race == null
                    ? includedVariants.OrderBy(v => v.DisplayOrder).Select(v => v.Name).ToList()
                    : new List<string>(),
                FileName        = uploadBatch.FileName,
                ParsedResults   = processedResults,
                TotalRows       = processedResults.Count,
                ValidRows       = processedResults.Count(r => r.ValidationIssues.Count == 0),
                RowsWithIssues  = processedResults.Count(r => r.ValidationIssues.Count > 0)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming upload batch {BatchId}", batchId);
            return BadRequest($"Error resuming upload batch: {ex.Message}");
        }
    }

    /// <summary>List upload batches, optionally filtered by year.</summary>
    [HttpGet("batches")]
    [ProducesResponseType(typeof(List<UploadBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UploadBatchDto>>> GetBatches([FromQuery] int? year)
    {
        var query = _context.UploadBatches.AsQueryable();
        if (year.HasValue)
            query = query.Where(b => b.Race != null
                ? b.Race.Year == year.Value
                : b.RaceDate!.Value.Year == year.Value);

        var batches = await query
            .OrderByDescending(b => b.UploadedAt)
            .Select(b => new UploadBatchDto
            {
                UploadBatchId   = b.UploadBatchId,
                RaceId          = b.RaceId,
                RaceName        = b.Race != null
                    ? RaceProjections.DisplayName(b.Race.RaceVariant.RaceSeries.Name, b.Race.RaceVariant.Name, b.Race.RaceVariant.RaceSeries.Variants.Count)
                    : b.RaceSeries!.Name + " (multiple variants)",
                RaceDate        = b.Race != null ? b.Race.Date : b.RaceDate!.Value,
                IsGrandPrixRace = b.Race != null && b.Race.IsGrandPrixRace,
                RaceSeriesId    = b.Race != null ? b.Race.RaceVariant.RaceSeriesId : b.RaceSeriesId!.Value,
                RaceSeriesName  = b.Race != null ? b.Race.RaceVariant.RaceSeries.Name : b.RaceSeries!.Name,
                FileName        = b.FileName,
                FileType        = b.FileType,
                RecordsUploaded = b.RecordsUploaded,
                UploadedBy      = b.UploadedBy,
                UploadedAt      = b.UploadedAt,
                Status          = b.Status,
                ExtractionError = b.ExtractionError
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
            if (uploadBatch.Race != null && await RejectIfSeasonFinalizedAsync(uploadBatch.Race) is { } finalized)
                return finalized;

            var results = await _context.RaceResults
                .Where(r => r.UploadBatchId == batchId)
                .ToListAsync();
            _context.RaceResults.RemoveRange(results);
            _context.UploadBatches.Remove(uploadBatch);

            await _context.SaveChangesAsync();

            // Recompute gender places and GP points/standings from whatever results remain
            // for this race (a race can have results from more than one upload batch, e.g.
            // corrections or course-variant splits), not just this batch's own results.
            // A pending multi-variant batch has no race (or results) yet - nothing to recompute.
            if (uploadBatch.RaceId is { } raceId)
            {
                await CalculateGenderPlacesAsync(raceId);
                await _grandPrixCalculationService.RecalculateAfterResultsChangeAsync(raceId);
            }

            await transaction.CommitAsync();
            _extractionJobs.Cancel(batchId); // stop the LLM call if it's still extracting

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

    private Task<UploadBatch?> LoadBatchWithSeriesAsync(Guid batchId) =>
        _context.UploadBatches
            .Include(b => b.Race!).ThenInclude(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
            .Include(b => b.RaceSeries!).ThenInclude(s => s.Variants)
            .FirstOrDefaultAsync(b => b.UploadBatchId == batchId);

    /// <summary>The batch's series and race date (from its race, or for a multi-variant batch, held on the batch).</summary>
    private static (RaceSeries Series, DateOnly RaceDate)? ResolveSeriesAndDate(UploadBatch batch)
    {
        var series = batch.Race?.RaceVariant.RaceSeries ?? batch.RaceSeries;
        var raceDate = batch.Race?.Date ?? batch.RaceDate;
        return series != null && raceDate != null ? (series, raceDate.Value) : null;
    }

    /// <summary>
    /// Variants a batch's sections may be routed to: a multi-variant batch's picked variants (all
    /// of the series' if none were recorded, or they've since been merged away), otherwise all.
    /// </summary>
    private static List<RaceVariant> RoutingVariants(UploadBatch batch, RaceSeries series)
    {
        var included = batch.Race == null
            ? series.Variants.Where(v => batch.IncludedVariantIds.Contains(v.RaceVariantId)).ToList()
            : new List<RaceVariant>();
        return included.Count > 0 ? included : series.Variants.ToList();
    }

    /// <summary>Turn a batch's stored LLM JSON into validated, variant-routed, runner-matched rows.</summary>
    private async Task<List<ResultRow>> ProcessStoredExtractionAsync(UploadBatch batch, List<RaceVariant> routeTo, DateOnly raceDate)
    {
        var sections = _llmExtractionService.RehydrateSections(batch.RawLlmJson ?? "{}", batch.FileName);
        var results = await _resultsProcessingService.ProcessResultsAsync(sections);
        // A section labeled with a course outside routeTo stays unmatched so it stands out in Data Review.
        _resultsProcessingService.AssignVariants(results, routeTo);
        return await _runnerMatchingService.FindMatchesForResultsAsync(results, raceDate);
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

    /// <summary>
    /// A 409 response when the race counts toward a finalized Grand Prix season (changing its
    /// results would change locked standings), otherwise null.
    /// </summary>
    private async Task<ObjectResult?> RejectIfSeasonFinalizedAsync(Race race) =>
        race.IsGrandPrixRace && await _grandPrixCalculationService.IsSeasonFinalizedAsync(race.Year)
            ? Conflict(new { message = GrandPrixSeasonFinalizedException.MessageFor(race.Year) })
            : null;

    private async Task CalculateGenderPlacesAsync(Guid raceId)
    {
        var results = await _context.RaceResults
            .Where(r => r.RaceId == raceId && r.Status == ResultStatus.Finished)
            .OrderBy(r => r.Place)
            .ToListAsync();

        var males      = results.Where(r => r.Gender == Gender.Male).ToList();
        var females    = results.Where(r => r.Gender == Gender.Female).ToList();
        var nonbinary  = results.Where(r => r.Gender == Gender.Nonbinary).ToList();

        for (int i = 0; i < males.Count;     i++) males[i].PlaceGender     = i + 1;
        for (int i = 0; i < females.Count;   i++) females[i].PlaceGender   = i + 1;
        for (int i = 0; i < nonbinary.Count; i++) nonbinary[i].PlaceGender = i + 1;

        await _context.SaveChangesAsync();
    }
}
