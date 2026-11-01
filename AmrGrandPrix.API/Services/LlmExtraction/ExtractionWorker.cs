using AmrGrandPrix.API.Data;
using AmrGrandPrix.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AmrGrandPrix.API.Services.LlmExtraction;

/// <summary>
/// Background service that drains <see cref="ExtractionJobs"/>, one batch at a time, so a slow
/// LLM extraction never holds an HTTP request open (Cloudflare cuts those off at 100 s). On
/// startup it re-queues batches left <c>Extracting</c> by a restart mid-extraction.
/// </summary>
public class ExtractionWorker : BackgroundService
{
    private readonly ExtractionJobs _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(ExtractionJobs queue, IServiceScopeFactory scopeFactory, ILogger<ExtractionWorker> logger)
    {
        _queue        = queue;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // don't hold up host startup
        await RequeueInterruptedAsync(stoppingToken);

        try
        {
            await foreach (var batchId in _queue.DequeueAllAsync(stoppingToken))
                await ProcessAsync(batchId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; anything unfinished stays Extracting and is re-queued next start.
        }
    }

    private async Task ProcessAsync(Guid batchId, CancellationToken stoppingToken)
    {
        var job = _queue.Start(batchId);
        if (job == null)
            return; // deleted while queued

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Cancellation.Token);
            using var scope = _scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<ExtractionJobRunner>();
            await runner.RunAsync(batchId, job.ReportRows, linked.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Extraction for batch {BatchId} cancelled (batch deleted)", batchId);
        }
        catch (Exception ex)
        {
            // e.g. the batch was deleted between the LLM finishing and the save
            _logger.LogError(ex, "Unexpected error running extraction for batch {BatchId}", batchId);
        }
        finally
        {
            _queue.Complete(batchId);
        }
    }

    private async Task RequeueInterruptedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var interrupted = await context.UploadBatches
                .Where(b => b.Status == UploadStatus.Extracting)
                .OrderBy(b => b.UploadedAt)
                .Select(b => b.UploadBatchId)
                .ToListAsync(ct);

            foreach (var id in interrupted)
                _queue.Enqueue(id);

            if (interrupted.Count > 0)
                _logger.LogInformation("Re-queued {Count} interrupted extraction(s)", interrupted.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Couldn't re-queue interrupted extractions");
        }
    }
}
