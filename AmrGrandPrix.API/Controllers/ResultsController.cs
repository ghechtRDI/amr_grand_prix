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

            // LLM extraction
            ExtractionResult extraction;
            using (var stream = request.File.OpenReadStream())
                extraction = await _llmExtractionService.ExtractAsync(
                    stream, request.File.FileName, knownVariants, onlyTheseVariants, ct);

            var processedResults = await _resultsProcessingService.ProcessResultsAsync(extraction.Sections);
            // Only route to the selected variants - a section the LLM labeled with any other course
            // stays unmatched so it stands out in Data Review.
            _resultsProcessingService.AssignVariants(
                processedResults, onlyTheseVariants ? includedVariants : seriesVariants.ToList());
            processedResults = await _runnerMatchingService.FindMatchesForResultsAsync(processedResults, raceDate);

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
                "File {FileName} uploaded for race {RaceId} (series {RaceSeriesId}) by {UserId}. Extracted {Sections} section(s), {Rows} rows. Tokens: {In}in/{Out}out",
                request.File.FileName, race?.RaceId, request.RaceSeriesId, user?.Id,
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
        var uploadBatch = await _context.UploadBatches
            .Include(b => b.Race!).ThenInclude(r => r.RaceVariant).ThenInclude(v => v.RaceSeries).ThenInclude(s => s.Variants)
            .Include(b => b.RaceSeries!).ThenInclude(s => s.Variants)
            .FirstOrDefaultAsync(b => b.UploadBatchId == batchId);

        if (uploadBatch == null)
            return NotFound($"Upload batch {batchId} not found");

        if (uploadBatch.Status != UploadStatus.Pending)
            return BadRequest($"Only pending uploads can be resumed (batch is {uploadBatch.Status})");

        if (string.IsNullOrEmpty(uploadBatch.RawLlmJson))
            return BadRequest("This upload batch has no stored extraction data to resume from");

        try
        {
            var race = uploadBatch.Race;
            var series = race?.RaceVariant.RaceSeries ?? uploadBatch.RaceSeries;
            var raceDate = race?.Date ?? uploadBatch.RaceDate;
            if (series == null || raceDate == null)
                return BadRequest("This upload batch isn't linked to a race or race series");

            // A pending multi-variant batch routes to the variants picked at upload (all of the
            // series' if none were recorded, or they've since been merged away).
            var includedVariants = race == null
                ? series.Variants.Where(v => uploadBatch.IncludedVariantIds.Contains(v.RaceVariantId)).ToList()
                : new List<RaceVariant>();
            if (includedVariants.Count == 0)
                includedVariants = series.Variants.ToList();

            var sections = _llmExtractionService.RehydrateSections(uploadBatch.RawLlmJson, uploadBatch.FileName);
            var processedResults = await _resultsProcessingService.ProcessResultsAsync(sections);
            _resultsProcessingService.AssignVariants(processedResults, includedVariants);
            processedResults = await _runnerMatchingService.FindMatchesForResultsAsync(processedResults, raceDate.Value);

            return Ok(new ResumeBatchResponse
            {
                UploadBatchId   = uploadBatch.UploadBatchId,
                RaceId          = race?.RaceId,
                RaceName        = series.Name,
                RaceDate        = raceDate.Value,
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
