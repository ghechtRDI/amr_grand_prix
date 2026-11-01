using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services.LlmExtraction;

/// <summary>
/// Runs one upload batch's LLM extraction (from the text stored at upload time) and records the
/// outcome on the batch: <c>Pending</c> with the raw LLM JSON on success - the same state the
/// wizard's review step and "Resume" already read - or <c>Failed</c> with the error.
/// </summary>
public class ExtractionJobRunner
{
    private const int MaxErrorLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly ILlmExtractionService _llm;
    private readonly ILogger<ExtractionJobRunner> _logger;

    public ExtractionJobRunner(ApplicationDbContext context, ILlmExtractionService llm, ILogger<ExtractionJobRunner> logger)
    {
        _context = context;
        _llm     = llm;
        _logger  = logger;
    }

    /// <exception cref="OperationCanceledException">
    /// <paramref name="ct"/> fired (shutdown or the batch was deleted); the batch is left as is.
    /// </exception>
    public async Task RunAsync(Guid batchId, Action<int>? onRowsExtracted, CancellationToken ct)
    {
        var batch = await _context.UploadBatches.FirstOrDefaultAsync(b => b.UploadBatchId == batchId, ct);
        if (batch is not { Status: UploadStatus.Extracting })
            return; // deleted, or already handled

        try
        {
            if (string.IsNullOrWhiteSpace(batch.ExtractedText))
                throw new InvalidOperationException("No file text was stored for this upload; please upload the file again.");

            var started = DateTime.UtcNow;
            var extraction = await _llm.ExtractFromTextAsync(
                batch.ExtractedText, batch.FileName, batch.KnownVariants, batch.OnlyTheseVariants, onRowsExtracted, ct);

            batch.RawLlmJson      = extraction.RawModelJson;
            batch.LlmModel        = extraction.LlmModel;
            batch.LlmInputTokens  = extraction.InputTokens;
            batch.LlmOutputTokens = extraction.OutputTokens;
            batch.RecordsUploaded = extraction.Sections.Sum(s => s.Rows.Count);
            batch.Status          = UploadStatus.Pending;

            _logger.LogInformation(
                "Extracted {Sections} section(s), {Rows} rows from {FileName} (batch {BatchId}) in {Seconds:F0}s. Tokens: {In}in/{Out}out",
                extraction.Sections.Count, batch.RecordsUploaded, batch.FileName, batchId,
                (DateTime.UtcNow - started).TotalSeconds, extraction.InputTokens, extraction.OutputTokens);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed for {FileName} (batch {BatchId})", batch.FileName, batchId);
            batch.Status          = UploadStatus.Failed;
            batch.ExtractionError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
        }

        // Save the outcome even if cancellation arrives now - the LLM work is already paid for.
        await _context.SaveChangesAsync(CancellationToken.None);
    }
}
